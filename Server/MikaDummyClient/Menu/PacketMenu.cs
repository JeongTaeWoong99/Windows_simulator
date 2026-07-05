using System;
using System.Collections.Generic;
using MikaProtocol;

namespace MikaDummyClient
{
    /// <summary>
    /// 보낼 패킷을 번호로 선택하고 필요한 필드를 입력받아 송신하는 메뉴 루프.
    /// 액션을 리스트에 등록하면 번호가 자동으로 부여된다(레지스트리 방식).
    /// 새 패킷을 시험하려면 <see cref="_actions"/>에 항목 하나만 추가하면 된다.
    /// </summary>
    public sealed class PacketMenu
    {
        private readonly List<ClientAction> _actions;

        public PacketMenu()
        {
            _actions = new List<ClientAction>
            {
                new ClientAction("Echo (채팅)", SendEcho),
                new ClientAction("Ping", SendPing),
                new ClientAction("Login", SendLogin),
            };
        }

        /// <summary>
        /// 메뉴를 반복 표시하며 번호 입력을 처리한다. 0을 입력하면 종료한다.
        /// </summary>
        public void Run()
        {
            while (true)
            {
                PrintMenu();

                Console.Write("Select > ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (!int.TryParse(input.Trim(), out int choice))
                {
                    Console.WriteLine("[Client] 숫자를 입력하세요.\n");
                    continue;
                }

                if (choice == 0)
                    break;

                if (choice < 1 || choice > _actions.Count)
                {
                    Console.WriteLine("[Client] 존재하지 않는 번호입니다.\n");
                    continue;
                }

                _actions[choice - 1].Execute();
                Console.WriteLine();
            }

            Console.WriteLine("[Client] 서버와 연결을 해제하고 종료합니다.");
        }

        private void PrintMenu()
        {
            Console.WriteLine("=== 보낼 패킷 선택 ===");
            for (int i = 0; i < _actions.Count; i++)
                Console.WriteLine($"{i + 1}) {_actions[i].Label}");
            Console.WriteLine("0) 종료");
        }

        // --- 각 패킷 액션 ---

        private void SendEcho()
        {
            Console.Write("보낼 메시지 > ");
            string message = Console.ReadLine() ?? "";
            NetworkManager.Instance.Send(new C_EchoRequest { Message = message });
        }

        private void SendPing()
        {
            NetworkManager.Instance.Send(new C_PingRequest());
        }

        private void SendLogin()
        {
            Console.Write("로그인 Id > ");
            string id = Console.ReadLine() ?? "";
            NetworkManager.Instance.Send(new C_LoginRequest { Id = id });
        }
    }
}
