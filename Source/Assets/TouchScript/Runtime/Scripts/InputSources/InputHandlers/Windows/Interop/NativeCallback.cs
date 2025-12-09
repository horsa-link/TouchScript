#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
using UnityEngine;

namespace TouchScript.InputSources.InputHandlers.Interop
{
    /// <summary>
    /// The method delegate used to pass data from the native DLL.
    /// </summary>
    /// <param name="id">Pointer id.</param>
    /// <param name="evt">Current event.</param>
    /// <param name="type">Pointer type.</param>
    /// <param name="position">Pointer position.</param>
    /// <param name="data">Pointer data.</param>
    delegate void NativePointerDelegate(int id, PointerEvent evt, PointerType type, Vector2 position, PointerData data);

    /// <summary>
    /// The method delegate used to pass log messages from the native DLL.
    /// </summary>
    /// <param name="log">The log message.</param>
    delegate void NativeLog([MarshalAs(UnmanagedType.BStr)] string log);
}
#endif