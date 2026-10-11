#if UNITY_5_3_OR_NEWER

using System;
using UnityEngine;
using MikaProtocol;
using Utils;

namespace MikaNetwork
{
    public class NetworkManager : SingletonMonoBehaviour<NetworkManager>
    {
        private readonly MikaClient _client = new();
        private readonly ServerPacketManager _packetManager = new();

        protected override void Awake()
        {
            base.Awake(); // Singleton 등록

            Debug.Log("NetworkManager is Awaken");
        }

        public async void Start()
        {
            _client.PacketReceived += (session, data) =>
            {
                _packetManager.OnRecvPacket(session, data, job =>
                {
                    NetworkMessageQueue.Instance.Push(job);
                });

                return default;
            };

            // 빌드는 ServerSettings.json, 에디터는 메뉴에서 고른 곳 — ServerEndpoint
            var (host, port) = ServerEndpoint.Resolve();
            Debug.Log($"서버 접속 {host}:{port}");
            await _client.ConnectAsync(host, port);
        }

        void Update()
        {
            NetworkMessageQueue.Instance.Flush();
        }

        public void Send<T>(T packet) where T : IPacket
        {
            _client.Send(packet);
        }
    }
}

#endif