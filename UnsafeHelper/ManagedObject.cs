using IlyfairyLib.Unsafe.Internal;
using System;
using System.Runtime.InteropServices;

namespace IlyfairyLib.Unsafe;

/// <summary>
/// 自动回收的托管对象
/// </summary>
/// <typeparam name="T"></typeparam>
public unsafe class ManagedObject<T> where T : class
{
    /// <summary>
    /// 前(nint)字节为TypeHandle
    /// </summary>
    private readonly byte[] data;
    public int Size => data.Length;
    public nint* Handle => (nint*)(UnsafeHelper.GetObjectRawDataAddress(data) + 8);
    public T Object { get; private set; }
    private readonly GCHandle gcHandle;
    private ManagedObject(byte[] data, object obj)
    {
        Object = System.Runtime.CompilerServices.Unsafe.As<T>(obj);
        this.data = data;
    }
    public ManagedObject(int size)
    {
        if (size <= 8) size = 8;
        size += 8;
        data = new byte[size];
        var handle = Handle;
        *handle = typeof(T).TypeHandle.Value;
        Object = IL.As<T>(handle);
        gcHandle = GCHandle.Alloc(Object);
    }
    ~ManagedObject()
    {
        gcHandle.Free();
    }
    public Span<T> GetDataSpan<T>() where T : unmanaged => new((byte*)Handle + sizeof(nint), Size / sizeof(T));
    public void ChangeType(Type type) => *Handle = type.TypeHandle.Value;
    public void ChangeType<T>() => *Handle = typeof(T).TypeHandle.Value;
    public ManagedObject<TTo> As<TTo>() where TTo : class => System.Runtime.CompilerServices.Unsafe.As<ManagedObject<TTo>>(this);
}
