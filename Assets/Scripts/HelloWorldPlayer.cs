// HelloWorldPlayer.cs
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

        public override void OnNetworkSpawn()
        {
            // Si este cliente es el propietario, solicita un Move inicial
            if (IsOwner)
            {
                Move();
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
    }
}
