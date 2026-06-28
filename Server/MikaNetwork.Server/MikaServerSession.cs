using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using MikaNetwork.Core.Interfaces;
using MikaNetwork.Core.Network;

namespace MikaNetwork.Server;

public sealed class MikaServerSession : ISession
{
    /// <summary>
    /// _sequenceKey와 CreateSessionId()를 통해 Unique한 SessionId를 발급
    /// </summary>
    private readonly Socket                             _socket;
    private readonly Channel<ReadOnlyMemory<byte>>      _sendQueue;
    private readonly MikaRecvBuffer                     _recvBuffer;
    private readonly CancellationTokenSource            _cts;

    private const int SendQueueCapacity = 1024;
    private const int MaxRecvBufferSize = ushort.MaxValue;
    private const int RecvChunkSize     = 4096;
    
    public EndPoint? RemoteEndPoint => _socket.RemoteEndPoint;
    public long SessionId { get; init; }
    public bool IsConnected { get; private set; } = false;
    
    public event Func<ISession, ReadOnlyMemory<byte>, ValueTask>? Received;
    public event Action<ISession>? Disconnected;
    public event Action<ISession>? Connected;
    
    public MikaServerSession(Socket socket, long sessionId)
    {
        _socket = socket;
        _sendQueue = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(SendQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        _recvBuffer = new MikaRecvBuffer(MaxRecvBufferSize);
        _cts = new CancellationTokenSource();
        SessionId = sessionId;
    }


    public void Send(ReadOnlyMemory<byte> data)
    {
        if(!_sendQueue.Writer.TryWrite(data)) // 송신 Queue에 넣는데 실패하면, 연결 끊음 
            Disconnect(); 
    }


    public void Disconnect()
    {
        if (!IsConnected)
            return;

        IsConnected = false;

        try
        {
            _socket?.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        
        Disconnected?.Invoke(this);
        Dispose();
    }
    
    public void Dispose()
    {
        _socket.Dispose();
        _cts.Cancel();
    }
    //
    //

    public async Task StartAsync()
    {
        if (IsConnected)
            return;
        
        IsConnected = true;
        Connected?.Invoke(this);
        
        try
        {
            await Task.WhenAll(ReceiveLoop(), SendLoop());
        }
        finally
        {
            Disconnect();
        }
    }

    public async Task OnReceived(ReadOnlyMemory<byte> data)
    {
        var handler = Received;

        if (handler is null) return;
        
        await handler(this, data);
    }
    

    private async Task ReceiveLoop()
    {
        try
        {
            while (IsConnected)
            {
                // 여기서 recvBuffer에 직접쓸 공간 가져오기 새 할당 XX
                var buffer =_recvBuffer.GetWritableMemory(RecvChunkSize);
                int received = await _socket.ReceiveAsync(buffer); // 연결 끊기면 예외 throw

                if (received == 0)
                {
                    break;
                }

                _recvBuffer.AdvanceWrite(received);
                while (_recvBuffer.ReadableBytes >= MikaPacketBuilder.HeaderSize) // PacketHeader
                {
                    var size = MikaPacketBuilder.ReadSize(_recvBuffer.GetReadableSpan());

                    // 읽은 사이즈가 Header보다 작거나, 패킷 사이즈보다 클 경우 차단
                    if (size < MikaPacketBuilder.HeaderSize || size > MikaPacketBuilder.MaxPacketSize)
                    {
                        return;
                    }

                    if (size <= _recvBuffer.ReadableBytes)
                    {
                        var data = MikaPacketBuilder.ReadPacket(_recvBuffer.GetReadableSpan(), size);
                        _recvBuffer.AdvanceRead(size);
                        await OnReceived(data);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception)
        {
            // 강제 종료(RST)·소켓 오류 등 → finally에서 Disconnect 처리
        }
        finally
        {
            Disconnect();
        }
    }

    private async Task SendLoop()
    {
        try
        {
            while (await _sendQueue.Reader.WaitToReadAsync(_cts.Token))
            {
                while (_sendQueue.Reader.TryRead(out var data))
                {
                    if (!IsConnected) return;

                    await _socket.SendAsync(data, SocketFlags.None);
                }
            }
        }
        catch (Exception)
        {
            Disconnect();
        }
    }


    
    
}