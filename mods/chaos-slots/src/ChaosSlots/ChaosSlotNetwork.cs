using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace ChaosSlots
{
    internal static class ChaosSlotNetwork
    {
        private const string CapabilityKey = "dev.pikac.chaos-slots.live-protocol";
        private const string CustomDataSaveKey = "GameSettingsManager::customData";
        private const string CapabilityPrefix = "1:";
        private const byte ProtocolVersion = 1;

        private readonly struct HelloV1 : NetworkMessage
        {
            internal HelloV1(byte version) => Version = version;
            internal byte Version { get; }
        }

        private readonly struct MoveV1 : NetworkMessage
        {
            internal MoveV1(uint roomNetId, SlotMoveRequest request)
            {
                RoomNetId = roomNetId;
                Request = request;
            }

            internal uint RoomNetId { get; }
            internal SlotMoveRequest Request { get; }
        }

        private sealed class Peer
        {
            internal ulong LastSequence;
        }

        private static readonly Dictionary<NetworkConnectionToClient, Peer> Peers = new();
        private static bool _serving;
        private static string _serverCapability;
        private static GameSettingsManager _advertisedSettings;
        private static NetworkConnection _helloConnection;
        private static string _helloCapability;
        private static ulong _nextSequence;

        internal static void Initialize()
        {
            Writer<HelloV1>.write = (writer, message) => writer.WriteByte(message.Version);
            Reader<HelloV1>.read = reader => new HelloV1(reader.ReadByte());
            Writer<MoveV1>.write = WriteMove;
            Reader<MoveV1>.read = ReadMove;
            if (NetworkServer.active)
                StartServer();
        }

        internal static bool IsEnabledFor(DewPlayer player)
        {
            if (player == null || player.hero == null)
                return false;
            if (NetworkServer.active)
                return player == DewPlayer.local
                    || (player.connectionToClient != null && Peers.ContainsKey(player.connectionToClient));
            return player == DewPlayer.local && GuestCanSend();
        }

        internal static void Poll()
        {
            var settings = NetworkedManagerBase<GameSettingsManager>.softInstance;
            if (NetworkServer.active)
            {
                if (_serving && !NetworkServer.dontListen && settings != null && settings.isServer
                    && settings.state == GameState.InGame)
                {
                    if (ReadCapability(settings) != _serverCapability)
                        settings.customData[CapabilityKey] = _serverCapability;
                    _advertisedSettings = settings;
                }
                else
                {
                    ClearAdvertisement();
                }
                return;
            }

            if (!NetworkClient.isConnected)
            {
                _helloConnection = null;
                _helloCapability = null;
                return;
            }
            if (!NetworkClient.ready)
                return;

            string capability = ReadCapability(settings);
            if (capability == null || (NetworkClient.connection == _helloConnection && capability == _helloCapability))
                return;

            _helloConnection = NetworkClient.connection;
            _helloCapability = capability;
            // Keep increasing across a module reload on the same live connection.
            _nextSequence = Math.Max(_nextSequence, (ulong)DateTime.UtcNow.Ticks);
            NetworkClient.Send(new HelloV1(ProtocolVersion), Channels.Reliable);
        }

        internal static bool RequestMove(DewPlayer player, GemLocation origin, HeroSkillLocation target)
        {
            if (NetworkServer.active)
                return ChaosSlotState.TryMove(player, origin, target);
            if (!GuestCanSend())
                return false;

            uint roomNetId = CurrentRoomNetId();
            if (roomNetId == 0u || !ChaosSlotState.TryCreateRequest(player, origin, target, ++_nextSequence, out var request))
                return false;

            NetworkClient.Send(new MoveV1(roomNetId, request), Channels.Reliable);
            return true;
        }

        private static bool GuestCanSend() =>
            NetworkClient.ready && NetworkClient.isConnected && _helloConnection != null
            && _helloConnection == NetworkClient.connection
            && ReadCapability(NetworkedManagerBase<GameSettingsManager>.softInstance) == _helloCapability;

        private static string ReadCapability(GameSettingsManager settings)
        {
            if (settings != null && settings.customData.TryGetValue(CapabilityKey, out string capability)
                && capability != null && capability.StartsWith(CapabilityPrefix, StringComparison.Ordinal))
                return capability;
            return null;
        }

        private static uint CurrentRoomNetId()
        {
            var zone = NetworkedManagerBase<ZoneManager>.softInstance;
            var room = zone == null ? null : zone.currentRoom;
            return room == null ? 0u : room.netId;
        }

        private static void StartServer()
        {
            Peers.Clear();
            _serving = true;
            _advertisedSettings = null;
            _serverCapability = CapabilityPrefix + Guid.NewGuid().ToString("N");
            NetworkServer.ReplaceHandler<HelloV1>(OnHello, requireAuthentication: true);
            NetworkServer.ReplaceHandler<MoveV1>(OnMove, requireAuthentication: true);
        }

        private static void OnHello(NetworkConnectionToClient sender, HelloV1 message)
        {
            if (_serving && message.Version == ProtocolVersion && !Peers.ContainsKey(sender))
                Peers.Add(sender, new Peer());
        }

        private static void OnMove(NetworkConnectionToClient sender, MoveV1 message)
        {
            if (!_serving || !sender.isReady || !Peers.TryGetValue(sender, out var peer))
                return;

            ulong lastSequence = peer.LastSequence;
            var request = message.Request;
            if (request.Sequence <= lastSequence)
                return;
            peer.LastSequence = request.Sequence;
            if (message.RoomNetId == 0u || message.RoomNetId != CurrentRoomNetId())
                return;

            ChaosSlotState.TryMoveFromRequest(sender.GetPlayer(), request, lastSequence);
        }

        internal static void ShutDown()
        {
            ClearAdvertisement();
            // Leave disabled handlers to drain messages in flight. Mirror clears its handlers on server shutdown,
            // and disconnects a guest immediately if it receives a message for an unregistered message ID.
            ResetServer();
            _helloConnection = null;
            _helloCapability = null;
        }

        private static void ClearAdvertisement()
        {
            if (NetworkServer.active && _advertisedSettings != null && _advertisedSettings.isServer
                && ReadCapability(_advertisedSettings) == _serverCapability)
                _advertisedSettings.customData.Remove(CapabilityKey);
            _advertisedSettings = null;
        }

        private static void ResetServer()
        {
            _serving = false;
            _advertisedSettings = null;
            _serverCapability = null;
            Peers.Clear();
        }

        private static void WriteMove(NetworkWriter writer, MoveV1 message)
        {
            var request = message.Request;
            writer.WriteUInt(message.RoomNetId);
            writer.WriteByte((byte)request.Source);
            writer.WriteInt(request.SourceIndex);
            writer.WriteByte((byte)request.Destination);
            writer.WriteInt(request.ExpectedSourceBonus);
            writer.WriteInt(request.ExpectedDestinationBonus);
            writer.WriteUInt(request.ExpectedGemNetId);
            writer.WriteULong(request.Sequence);
        }

        private static MoveV1 ReadMove(NetworkReader reader)
        {
            uint roomNetId = reader.ReadUInt();
            var request = new SlotMoveRequest((MemorySlot)reader.ReadByte(), reader.ReadInt(),
                (MemorySlot)reader.ReadByte(), reader.ReadInt(), reader.ReadInt(), reader.ReadUInt(), reader.ReadULong());
            return new MoveV1(roomNetId, request);
        }

        [HarmonyPatch(typeof(DewNetworkManager), nameof(DewNetworkManager.OnStartHost))]
        private static class StartHostPatch
        {
            private static void Postfix() => StartServer();
        }

        [HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
        private static class StopServerPatch
        {
            private static void Postfix() => ResetServer();
        }

        [HarmonyPatch(typeof(DewNetworkManager), nameof(DewNetworkManager.OnServerDisconnect))]
        private static class DisconnectPatch
        {
            private static void Postfix(NetworkConnectionToClient conn) => Peers.Remove(conn);
        }

        [HarmonyPatch(typeof(DewPersistence), nameof(DewPersistence.SerializeGameObject))]
        private static class SavePatch
        {
            private static void Postfix(GameObject gobj, Dictionary<string, string> __result)
            {
                if (!__result.ContainsKey(CustomDataSaveKey) || !gobj.TryGetComponent<GameSettingsManager>(out var settings)
                    || !settings.customData.ContainsKey(CapabilityKey))
                    return;

                var durableData = new Dictionary<string, string>(settings.customData);
                durableData.Remove(CapabilityKey);
                __result[CustomDataSaveKey] = DewPersistence.ToJson(durableData);
            }
        }
    }
}
