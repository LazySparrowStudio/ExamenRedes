using UnityEngine;

[ExecuteAlways]
public class ZoneVisualizer : MonoBehaviour
{
    public float zoneWidth = 3f;     // Mitad del ancho total de una zona (asume un plano de 9 de ancho)
    public float planeLength = 10f;   // Largo del plano (eje Z)

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        DrawVerticalLine(-zoneWidth);  // Línea entre Zona1 y Centro

        Gizmos.color = Color.red;
        DrawVerticalLine(zoneWidth);   // Línea entre Centro y Zona2
    }

    private void DrawVerticalLine(float x)
    {
        Vector3 from = new Vector3(x, 0.01f, -planeLength / 2);
        Vector3 to = new Vector3(x, 0.01f, planeLength / 2);
        Gizmos.DrawLine(from, to);
    }
}
