using Mirror;

namespace Networking
{
    public struct AuthRequest : NetworkMessage
    {
        public string Password;
    }

    public struct AuthResponse : NetworkMessage
    {
        public bool Success;
    }

    public class PasswordAuthenticator : NetworkAuthenticator
    {
        public string ServerPassword;
        public string ClientPassword;

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<AuthRequest>(OnAuthRequest, false);
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<AuthResponse>(OnAuthResponse, false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<AuthRequest>();
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<AuthResponse>();
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            // wait
        }

        public override void OnClientAuthenticate()
        {
            NetworkClient.Send(new AuthRequest { Password = ClientPassword ?? string.Empty });
        }

        private void OnAuthRequest(NetworkConnectionToClient conn, AuthRequest msg)
        {
            var success = string.IsNullOrEmpty(ServerPassword) || msg.Password == ServerPassword;

            conn.Send(new AuthResponse { Success = success });

            if (success) ServerAccept(conn);
            else ServerReject(conn);
        }

        private void OnAuthResponse(AuthResponse msg)
        {
            if (msg.Success)
            {
                ClientAccept();
            }
            else
            {
                ClientReject();

                if (NetworkManager.singleton?.EosLobby?.ConnectedToLobby == true)
                {
                    NetworkManager.singleton.EosLobby.LeaveLobby();
                }
            }
        }
    }
}