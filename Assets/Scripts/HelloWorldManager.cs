// HelloWorldManager.cs
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace HelloWorld
{
    public enum Team
    {
        None,
        Team1,
        Team2
    }

    /// <summary>
    /// Adjuntar al mismo GameObject que tu NetworkManager.
    /// Maneja la GUI del lobby, la aprobación de conexiones y la asignación de color por jugador.
    /// Ahora con gestión básica de equipos.
    /// </summary>
    public class HelloWorldManager : MonoBehaviour
    {
        private NetworkManager m_NetworkManager;

        private static readonly List<Color> MasterColors = new List<Color>
        {
            Color.red,
            Color.blue,
            Color.green,
            Color.yellow,
            Color.magenta,
            Color.cyan
        };

        private readonly List<Color> _colorPool = new List<Color>();
        private readonly Dictionary<ulong, Color> _assignedColors = new Dictionary<ulong, Color>();

        // Diccionario que asocia cada equipo con su lista de clientIds
        public Dictionary<Team, List<ulong>> teamMembers = new Dictionary<Team, List<ulong>>()
        {
            { Team.Team1, new List<ulong>() },
            { Team.None, new List<ulong>() },
            { Team.Team2, new List<ulong>() }
        };

        // Max jugadores por equipo editable en GUI
        public int maxPlayersPerTeam = 2;

        public static HelloWorldManager Instance { get; private set; }

        private string maxPlayersInput;

        private void Awake()
        {
            Instance = this;
            m_NetworkManager = GetComponent<NetworkManager>();

            _colorPool.AddRange(MasterColors);

            m_NetworkManager.NetworkConfig.ConnectionApproval = true;
            m_NetworkManager.ConnectionApprovalCallback += ApproveOrReject;
            m_NetworkManager.OnClientConnectedCallback += OnClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            maxPlayersInput = maxPlayersPerTeam.ToString();
        }

        private void OnGUI()
        {
            Rect areaRect = new Rect(10, 10, 350, 400);

            // Dibuja fondo semitransparente negro para mejorar legibilidad
            Color prevColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.Box(areaRect, GUIContent.none);
            GUI.color = prevColor;

            GUILayout.BeginArea(areaRect);

            if (!m_NetworkManager.IsServer && !m_NetworkManager.IsClient)
            {
                GUILayout.Label("Iniciar modo:");

                if (GUILayout.Button("Host"))
                    m_NetworkManager.StartHost();

                if (GUILayout.Button("Client"))
                    m_NetworkManager.StartClient();

                if (GUILayout.Button("Server"))
                    m_NetworkManager.StartServer();
            }
            else if (m_NetworkManager.IsServer)
            {
                GUILayout.Label("Equipos y Jugadores");

                GUILayout.BeginHorizontal();
                GUILayout.Label("Max Jugadores por Equipo: ", GUILayout.Width(180));
                maxPlayersInput = GUILayout.TextField(maxPlayersInput, GUILayout.Width(50));
                if (GUILayout.Button("Aplicar", GUILayout.Width(60)))
                {
                    if (int.TryParse(maxPlayersInput, out int newMax) && newMax > 0)
                    {
                        maxPlayersPerTeam = newMax;
                    }
                    else
                    {
                        Debug.LogWarning("Valor inválido para máximo jugadores.");
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.Label("Equipo 1 (Izquierda):");
                DisplayTeamPlayers(Team.Team1);

                GUILayout.Space(10);

                GUILayout.Label("Centro (Sin equipo):");
                DisplayTeamPlayers(Team.None);

                GUILayout.Space(10);

                GUILayout.Label("Equipo 2 (Derecha):");
                DisplayTeamPlayers(Team.Team2);
            }
            else
            {
                GUILayout.Label("Conectado como cliente...");
            }

            GUILayout.EndArea();
        }

        private void DisplayTeamPlayers(Team team)
        {
            bool foundAny = false;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObj == null) continue;

                var player = playerObj.GetComponent<HelloWorldPlayer>();
                if (player == null) continue;

                if (player.playerTeam.Value == team)
                {
                    foundAny = true;

                    Color col = player.PlayerColor.Value;

                    GUILayout.BeginHorizontal();
                    GUI.color = col;
                    GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(20));
                    GUI.color = Color.white;
                    GUILayout.Label($"Client {clientId}", GUILayout.Width(120));
                    GUILayout.EndHorizontal();
                }
            }

            if (!foundAny)
            {
                GUILayout.Label(" - Vacío -");
            }
        }


        private void ApproveOrReject(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            // Limite total = suma equipos y neutros? Aquí solo limita por colores disponibles
            if (_assignedColors.Count >= MasterColors.Count)
            {
                res.Approved = false;
                res.Reason = "Lobby lleno (máx. " + MasterColors.Count + " jugadores).";
                return;
            }

            res.Approved = true;
            res.CreatePlayerObject = true;
            res.PlayerPrefabHash = null;
            res.Position = Vector3.zero;
            res.Rotation = Quaternion.identity;
        }

        private void OnClientConnected(ulong clientId)
        {
            AssignNewColor(clientId);

            // Nuevo jugador empieza sin equipo (centro)
            AddPlayerToTeam(clientId, Team.None);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_assignedColors.TryGetValue(clientId, out var color))
            {
                _assignedColors.Remove(clientId);
                _colorPool.Add(color);
            }

            RemovePlayerFromAllTeams(clientId);
        }

        public void AssignNewColor(ulong clientId)
        {
            if (_assignedColors.TryGetValue(clientId, out var old))
            {
                _colorPool.Add(old);
            }

            if (_colorPool.Count == 0)
            {
                Debug.LogWarning("¡No hay colores libres para asignar!");
                return;
            }

            Color newColor = _colorPool[0];
            _colorPool.RemoveAt(0);

            _assignedColors[clientId] = newColor;

            var player = m_NetworkManager.SpawnManager
                .GetPlayerNetworkObject(clientId)
                .GetComponent<HelloWorldPlayer>();

            player.PlayerColor.Value = newColor;
        }

        // Añade jugador a equipo (y elimina de otros equipos)
        public void AddPlayerToTeam(ulong clientId, Team team)
        {
            RemovePlayerFromAllTeams(clientId);

            if (!teamMembers.ContainsKey(team))
                teamMembers[team] = new List<ulong>();

            if (team == Team.Team1 || team == Team.Team2)
            {
                if (teamMembers[team].Count >= maxPlayersPerTeam)
                {
                    Debug.Log($"Equipo {team} lleno, no se añade el jugador {clientId}");
                    AddPlayerToTeam(clientId, Team.None); // Si está lleno, vuelve al centro
                    return;
                }
            }

            teamMembers[team].Add(clientId);

            // Cambia color en el jugador según equipo
            var player = m_NetworkManager.SpawnManager
                .GetPlayerNetworkObject(clientId)
                .GetComponent<HelloWorldPlayer>();

            switch (team)
            {
                case Team.Team1:
                    player.PlayerColor.Value = Color.blue;
                    break;
                case Team.Team2:
                    player.PlayerColor.Value = new Color(1f, 0.5f, 0f); // naranja
                    break;
                case Team.None:
                default:
                    player.PlayerColor.Value = Color.gray;
                    break;
            }
        }

        // Elimina jugador de todos los equipos
        public void RemovePlayerFromAllTeams(ulong clientId)
        {
            foreach (var team in teamMembers.Keys.ToList())
            {
                if (teamMembers[team].Contains(clientId))
                {
                    teamMembers[team].Remove(clientId);
                }
            }
        }
    }
}
