// HelloWorldManager.cs
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace HelloWorld
{
    /// <summary>
    /// Adjuntar al mismo GameObject que tu NetworkManager.
    /// Maneja la GUI del lobby, la aprobación de conexiones y la asignación de color por jugador.
    /// </summary>
    public class HelloWorldManager : MonoBehaviour
    {
        private NetworkManager m_NetworkManager;

        // Lista “maestra” de 6 colores distintos que repartimos.
        private static readonly List<Color> MasterColors = new List<Color>
        {
            Color.red,
            Color.blue,
            Color.green,
            Color.yellow,
            Color.magenta,
            Color.cyan
        };

        // Pool de trabajo de donde tomamos y devolvemos colores.
        private readonly List<Color> _colorPool = new List<Color>();

        // Asocia cada clientId con su color asignado.
        private readonly Dictionary<ulong, Color> _assignedColors = new Dictionary<ulong, Color>();

        private void Awake()
        {
            m_NetworkManager = GetComponent<NetworkManager>();

            // Inicializa el pool de trabajo con la lista maestra.
            _colorPool.AddRange(MasterColors);

            // Activa la aprobación de conexión antes de enganchar los callbacks.
            m_NetworkManager.NetworkConfig.ConnectionApproval = true;
            m_NetworkManager.ConnectionApprovalCallback += ApproveOrReject;
            m_NetworkManager.OnClientConnectedCallback += OnClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));

            // Si aún no somos cliente ni servidor, mostramos los botones Host/Client/Server.
            if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
            {
                if (GUILayout.Button("Host")) m_NetworkManager.StartHost();
                if (GUILayout.Button("Client")) m_NetworkManager.StartClient();
                if (GUILayout.Button("Server")) m_NetworkManager.StartServer();
            }
            else
            {
                // Si ya estamos en modo red, mostramos transporte y modo actual.
                var mode = m_NetworkManager.IsHost ? "Host"
                         : m_NetworkManager.IsServer ? "Server"
                                                     : "Client";
                GUILayout.Label("Transporte: " +
                    m_NetworkManager.NetworkConfig.NetworkTransport.GetType().Name);
                GUILayout.Label("Modo: " + mode);

                // Botón “Move”: el servidor mueve a todos; el cliente solicita su propio movimiento.
                if (GUILayout.Button(
                    m_NetworkManager.IsServer && !m_NetworkManager.IsClient
                        ? "Move"
                        : "Request Position Change"))
                {
                    if (m_NetworkManager.IsServer && !m_NetworkManager.IsClient)
                    {
                        // Servidor: iterar todos los clientes conectados
                        foreach (ulong uid in m_NetworkManager.ConnectedClientsIds)
                        {
                            var netObj = m_NetworkManager.SpawnManager
                                .GetPlayerNetworkObject(uid);
                            netObj.GetComponent<HelloWorldPlayer>().Move();
                        }
                    }
                    else
                    {
                        // Cliente: solo solicita en el jugador local
                        var local = m_NetworkManager.SpawnManager
                            .GetLocalPlayerObject()
                            .GetComponent<HelloWorldPlayer>();
                        local.Move();
                    }
                }

                // Botón “Change Color”: solo mostrado/ejecutable en clientes (incluido el host como cliente).
                if (m_NetworkManager.IsClient)
                {
                    if (GUILayout.Button("Change Color"))
                    {
                        var local = m_NetworkManager.SpawnManager
                            .GetLocalPlayerObject()
                            .GetComponent<HelloWorldPlayer>();
                        local.RequestColorChangeServerRpc();
                    }
                }
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Callback de aprobación de conexión: rechaza si ya hay ≥6 jugadores; de lo contrario acepta.
        /// </summary>
        private void ApproveOrReject(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            if (_assignedColors.Count >= MasterColors.Count)
            {
                res.Approved = false;
                res.Reason = "Lobby lleno (máx. 6 jugadores).";
                return;
            }

            res.Approved = true;
            res.CreatePlayerObject = true;
            res.PlayerPrefabHash = null;            // usa el prefab por defecto
            res.Position = Vector3.zero;    // posición de spawn
            res.Rotation = Quaternion.identity;
        }

        /// <summary>
        /// Ejecutado en el servidor cuando un cliente nuevo es aprobado y conectado.
        /// Asigna a ese cliente un color libre.
        /// </summary>
        private void OnClientConnected(ulong clientId)
        {
            AssignNewColor(clientId);
        }

        /// <summary>
        /// Ejecutado en el servidor cuando un cliente se desconecta.
        /// Recupera su color y lo devuelve al pool.
        /// </summary>
        private void OnClientDisconnected(ulong clientId)
        {
            if (_assignedColors.TryGetValue(clientId, out var color))
            {
                _assignedColors.Remove(clientId);
                _colorPool.Add(color);
            }
        }

        /// <summary>
        /// Helper en el servidor: recupera color antiguo,
        /// toma el siguiente libre, guarda la asignación
        /// y escribe directamente en el NetworkVariable del jugador.
        /// </summary>
        public void AssignNewColor(ulong clientId)
        {
            // 1) Si ya tenía un color, lo devuelve al pool.
            if (_assignedColors.TryGetValue(clientId, out var old))
            {
                _colorPool.Add(old);
            }

            // 2) Protección.
            if (_colorPool.Count == 0)
            {
                Debug.LogWarning("¡No hay colores libres para asignar!");
                return;
            }

            // 3) Extrae un nuevo color del pool.
            Color newColor = _colorPool[0];
            _colorPool.RemoveAt(0);

            // 4) Registra la asignación.
            _assignedColors[clientId] = newColor;

            // 5) Obtiene el objeto del jugador y escribe directamente en su NetworkVariable.
            var player = m_NetworkManager.SpawnManager
                .GetPlayerNetworkObject(clientId)
                .GetComponent<HelloWorldPlayer>();
            player.PlayerColor.Value = newColor;
        }
    }
}
