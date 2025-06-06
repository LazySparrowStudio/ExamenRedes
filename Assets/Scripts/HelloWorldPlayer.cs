// HelloWorldPlayer.cs
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HelloWorld
{
    /// <summary>
    /// Adjuntar al prefab de jugador (con NetworkObject y Renderer).
    /// Gestiona RPCs de posición y aplica cambios de color.
    /// </summary>
    public class HelloWorldPlayer : NetworkBehaviour
    {
        // =====================================================
        // 1) Sincronización de posición con un NetworkVariable
        // =====================================================
        public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();

        public NetworkVariable<Team> playerTeam = new NetworkVariable<Team>(Team.None);

        private static readonly Color team1Color = Color.blue;
        private static readonly Color team2Color = new Color(1.0f, 0.5f, 0f); // naranja
        private static readonly Color neutralColor = Color.gray;

        private int MaxTeamSize => HelloWorldManager.Instance.maxPlayersPerTeam;


        private readonly Vector3 centerPosition = new Vector3(0f, 1f, 0f); // Ajusta 1f según tu cápsula


        public override void OnNetworkSpawn()
        {
            // Si este cliente es el propietario, solicita un Move inicial
            if (IsOwner)
            {
                Move();
                PlayerColor.Value = neutralColor;
            }

            // Engancha el callback de cambio de color (ver más abajo)
            PlayerColor.OnValueChanged += OnColorChanged;
            // Aplica cualquier color ya definido antes de spawnear
            OnColorChanged(Color.white, PlayerColor.Value);
        }

        // Invocado por la GUI: directamente en servidor o vía RPC de cliente
        public void Move()
        {
            SubmitPositionRequestRpc();
        }

        // RPC Cliente→Servidor: el servidor elige posición aleatoria y la escribe en Position
        [Rpc(SendTo.Server)]
        private void SubmitPositionRequestRpc(RpcParams rpcParams = default)
        {
            Vector3 rnd = GetRandomPositionOnPlane();
            transform.position = rnd;
            Position.Value = rnd;
        }

        private static Vector3 GetRandomPositionOnPlane()
        {
            return new Vector3(
                Random.Range(-3f, 3f),
                1f,
                Random.Range(-3f, 3f)
            );
        }

        private void Update()
        {
            // Todos (cliente y servidor) siguen la posición networkeada
            transform.position = Position.Value;
        }

        // =====================================================
        // 2) Asignación de color con un NetworkVariable escribible por servidor
        // =====================================================
        public NetworkVariable<Color> PlayerColor =
            new NetworkVariable<Color>(
                writePerm: NetworkVariableWritePermission.Server
            );

        // RPC para que los clientes pidan un nuevo color al servidor.
        [ServerRpc(RequireOwnership = false)]
        public void RequestColorChangeServerRpc(ServerRpcParams rpcParams = default)
        {
            // Pide al manager reasignar color para este cliente
            ulong clientId = rpcParams.Receive.SenderClientId;
            var mgr = FindFirstObjectByType<HelloWorldManager>();
            mgr.AssignNewColor(clientId);
        }

        // Cada vez que PlayerColor.Value cambia, esto corre en todas las máquinas.
        private void OnColorChanged(Color oldColor, Color newColor)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null) return;

            // Clona el material para que el tinte no afecte a todas las instancias
            renderer.material = new Material(renderer.sharedMaterial);
            renderer.material.color = newColor;
        }
        //---------- MOVIMIENTO DEL JUGADOR AWSD ----------
        private void FixedUpdate()
        {
            if (!IsOwner) return; // Solo el jugador local puede controlar su movimiento

            Vector3 direction = new Vector3(
                Input.GetKey(KeyCode.D) ? 1 : Input.GetKey(KeyCode.A) ? -1 : 0,
                0,
                Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0
            );

            if (direction != Vector3.zero)
            {
                MoveRequestServerRpc(direction.normalized);
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                if (IsServer) // modo solo servidor
                {
                    TeleportAllToCenterServerRpc();
                }
                else
                {
                    TeleportToCenterServerRpc();
                }
            }
        }
        [ServerRpc]
        private void MoveRequestServerRpc(Vector3 direction)
        {
            float speed = 3f;
            Vector3 newPos = transform.position + direction * speed * Time.fixedDeltaTime;

            newPos.x = Mathf.Clamp(newPos.x, -5f, 5f);
            newPos.z = Mathf.Clamp(newPos.z, -5f, 5f);

            UpdateTeamByPosition(newPos, OwnerClientId);

            Position.Value = newPos;
        }

        [ServerRpc(RequireOwnership = false)]
        private void TeleportToCenterServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            var player = NetworkManager.Singleton.SpawnManager
                .GetPlayerNetworkObject(clientId)
                .GetComponent<HelloWorldPlayer>();

            player.RemoveFromTeam(clientId);
            player.Position.Value = centerPosition;
            player.PlayerColor.Value = neutralColor;
        }

        [ServerRpc(RequireOwnership = false)]
        private void TeleportAllToCenterServerRpc()
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var player = NetworkManager.Singleton.SpawnManager
                    .GetPlayerNetworkObject(clientId)
                    .GetComponent<HelloWorldPlayer>();

                player.RemoveFromTeam(clientId);
                player.Position.Value = centerPosition;
                player.PlayerColor.Value = neutralColor;
            }
        }

        private void UpdateTeamByPosition(Vector3 position, ulong clientId)
        {
            if (position.x < -2)
            {
                TryJoinTeam(Team.Team1, team1Color, clientId, ref position);
            }
            else if (position.x > 2)
            {
                TryJoinTeam(Team.Team2, team2Color, clientId, ref position);
            }
            else
            {
                RemoveFromTeam(clientId);
                PlayerColor.Value = neutralColor;
                playerTeam.Value = Team.None;
            }
        }

        private void TryJoinTeam(Team targetTeam, Color teamColor, ulong clientId, ref Vector3 position)
        {
            if (playerTeam.Value == targetTeam)
                return;

            var manager = HelloWorldManager.Instance;

            // Pide al manager que intente meter al jugador
            manager.AddPlayerToTeam(clientId, targetTeam);

            // El manager ya ajustará el color y actualizará playerTeam
            playerTeam.Value = targetTeam;
        }

        private void RemoveFromTeam(ulong clientId)
        {
            HelloWorldManager.Instance.RemovePlayerFromAllTeams(clientId);
            playerTeam.Value = Team.None;
        }
    }
}
