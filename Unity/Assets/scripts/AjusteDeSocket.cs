using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;

/// <summary>
/// Apaga el algoritmo de Nagle (TCP_NODELAY) en el socket que el cliente
/// WebSocket de Mono esconde adentro.
///
/// Por qué: el juego manda mensajes chicos y seguidos (30 posiciones por
/// segundo, el pong del jugador 2). Con Nagle encendido, un mensaje chico espera
/// a que el relay confirme (ACK) el anterior, y el sistema demora ese ACK hasta
/// 40 ms. En Linux el relay lo esquiva pidiendo TCP_QUICKACK, pero en Windows o
/// macOS ese truco no existe: ahí cada mensaje podía tardar 40 ms de más.
///
/// `ClientWebSocket` no deja pedir NoDelay (ServicePointManager no le afecta:
/// se probó), así que se busca el `Socket` por reflexión entre los campos del
/// objeto. Si la versión de Mono cambia y no se encuentra, no pasa nada: el
/// juego sigue como antes. No usa UnityEngine para probarse con Mono a secas.
/// </summary>
public static class AjusteDeSocket
{
    private const int MaximoDeObjetosRevisados = 4000;
    private const BindingFlags TodosLosCampos =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// Busca y apaga Nagle. True si encontró un socket y lo dejó con NoDelay.
    public static bool DesactivarNagle(object raiz)
    {
        Socket socket = BuscarSocket(raiz);
        if (socket == null) return false;
        try
        {
            socket.NoDelay = true;
            return socket.NoDelay;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// El primer `Socket` que haya dentro de `raiz` (campos, arreglos y listas,
    /// a cualquier profundidad), o null si no hay.
    public static Socket BuscarSocket(object raiz)
    {
        if (raiz == null) return null;
        var vistos = new HashSet<object>(ReferenceEqualityComparer.Instancia);
        var pendientes = new Queue<object>();
        pendientes.Enqueue(raiz);
        while (pendientes.Count > 0 && vistos.Count < MaximoDeObjetosRevisados)
        {
            object actual = pendientes.Dequeue();
            if (actual == null || !vistos.Add(actual)) continue;
            if (actual is Socket socket) return socket;
            foreach (object hijo in Hijos(actual))
                if (hijo != null && !vistos.Contains(hijo)) pendientes.Enqueue(hijo);
        }
        return null;
    }

    private static IEnumerable<object> Hijos(object objeto)
    {
        Type tipo = objeto.GetType();
        if (tipo.IsPrimitive || objeto is string || objeto is Delegate || objeto is Type) yield break;

        if (objeto is IEnumerable lista && !(objeto is IDictionary))
        {
            // Arreglos y listas: cada elemento. Los diccionarios internos de .NET se
            // revisan por campos, como cualquier otro objeto.
            IEnumerator enumerador;
            try { enumerador = lista.GetEnumerator(); }
            catch (Exception) { yield break; }
            int contados = 0;
            while (contados++ < 256)
            {
                bool hay;
                object elemento;
                try
                {
                    hay = enumerador.MoveNext();
                    elemento = hay ? enumerador.Current : null;
                }
                catch (Exception) { yield break; }
                if (!hay) yield break;
                if (elemento != null && !elemento.GetType().IsValueType) yield return elemento;
            }
            yield break;
        }

        for (Type t = tipo; t != null && t != typeof(object); t = t.BaseType)
        {
            FieldInfo[] campos;
            try { campos = t.GetFields(TodosLosCampos | BindingFlags.DeclaredOnly); }
            catch (Exception) { continue; }
            foreach (FieldInfo campo in campos)
            {
                if (campo.FieldType.IsPrimitive || campo.FieldType.IsEnum || campo.FieldType == typeof(string)) continue;
                object valor;
                try { valor = campo.GetValue(objeto); }
                catch (Exception) { continue; }
                if (valor != null && !valor.GetType().IsValueType) yield return valor;
            }
        }
    }

    /// Compara por referencia: dos objetos distintos con el mismo Equals no son el mismo nodo.
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instancia = new ReferenceEqualityComparer();
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object objeto) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(objeto);
    }
}
