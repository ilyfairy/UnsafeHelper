using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
//using IlyfairyLib.Unsafe.Internal;
using UnsafeCore = System.Runtime.CompilerServices.Unsafe;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
namespace IlyfairyLib.Unsafe
{
    internal sealed class RawData
    {
        public byte Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ObjectHeader
    {
#if TARGET_64BIT
        [FieldOffset(4)]
#else
        [FieldOffset(0)]
#endif
        public uint SyncBlockValue;
    }

    /// <summary>
    /// Unsafe的工具方法
    /// </summary>
    public static unsafe class UnsafeHelper
    {
        private static readonly Type RuntimeHelpersType;
        private static readonly Func<object, object>? AllocateUninitializedClone;
        private static readonly int m_fieldHandle_offset = -1; // RtFieldInfo中的m_fieldHandle的偏移地址

        private static readonly delegate* unmanaged<object, object> AllocateUninitializedClone2;

        //static UnsafeHelper()
        //{
        //    RuntimeHelpersType = typeof(RuntimeHelpers);
        //    AllocateUninitializedClone = (Func<object, object>?)RuntimeHelpersType.GetMethod("AllocateUninitializedClone", BindingFlags.Static | BindingFlags.NonPublic)?.CreateDelegate(typeof(Func<object, object>));

        //    //获取RtFieldInfo中的m_fieldHandle
        //    //var info = typeof(UnsafeHelper).GetField("m_fieldHandle_offset", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static); //只是为了获取一个RtFieldInfo实例
        //    //var m_fieldHandleInfo = info.GetType().GetField("m_fieldHandle", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        //    var m_fieldHandleInfo = typeof(FieldInfo).Assembly.GetType("System.Reflection.RtFieldInfo")?.GetField("m_fieldHandle", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        //    if (m_fieldHandleInfo == null) return;
        //    var addr = (IntPtr*)GetObjectRawDataAddress(m_fieldHandleInfo);
        //    var m_fieldHandle = (IntPtr)m_fieldHandleInfo.GetValue(m_fieldHandleInfo)!;

        //    //获取m_fieldHandle的偏移地址
        //    int size = (int)GetObjectRawDataSize(m_fieldHandleInfo);
        //    for (int i = 0; i < size; i += 1)
        //    {
        //        if (m_fieldHandle == addr[i])
        //        {
        //            m_fieldHandle_offset = i * sizeof(IntPtr);
        //            break;
        //        }
        //    }
        //}

        static UnsafeHelper()
        {
            RuntimeHelpersType = typeof(RuntimeHelpers);
            AllocateUninitializedClone = (Func<object, object>?)RuntimeHelpersType.GetMethod("AllocateUninitializedClone", BindingFlags.Static | BindingFlags.NonPublic)?.CreateDelegate(typeof(Func<object, object>));
            //获取RtFieldInfo中的m_fieldHandle
            //var info = typeof(UnsafeHelper).GetField("m_fieldHandle_offset", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static); //只是为了获取一个RtFieldInfo实例
            //var m_fieldHandleInfo = info.GetType().GetField("m_fieldHandle", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var m_fieldHandleInfo = typeof(FieldInfo).Assembly.GetType("System.Reflection.RtFieldInfo")?.GetField("m_fieldHandle", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (m_fieldHandleInfo == null) return;
            var addr = (nint*)GetRawDataPointer(m_fieldHandleInfo);
            var m_fieldHandle = (nint)m_fieldHandleInfo.GetValue(m_fieldHandleInfo)!;

            //获取m_fieldHandle的偏移地址
            int size = (int)GetRawDataSize(m_fieldHandleInfo);
            for (int i = 0; i < size; i += 1)
            {
                if (m_fieldHandle == addr[i])
                {
                    m_fieldHandle_offset = i * sizeof(nint);
                    break;
                }
            }
        }

        #region GetPointer
        /// <summary>
        /// 获取引用的地址
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="val"></param>
        /// <returns>address</returns>
        [Obsolete]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* GetPointer<T>(ref T val) => (byte*)UnsafeCore.AsPointer(ref val);
        //{
        //    fixed(void* p = &val)
        //    {
        //        return (IntPtr)p;
        //    }
        //}

        /// <summary>
        /// 获取对象实例的地址(Handle的位置)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* GetPointer(object obj)
        {
            return UnsafeCore.AsPointer(ref UnsafeCore.Add(ref GetRawDataReference(obj), -1));
        }
        #endregion

        #region GetRawData
        /// <summary>
        /// 获取对象数据区域的地址
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetRawDataReference(object obj) => ref UnsafeCore.As<RawData>(obj).Data;

        /// <summary>
        /// 获取对象数据区域的地址
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* GetRawDataPointer(object obj) => (byte*)UnsafeCore.AsPointer(ref GetRawDataReference(obj));
        #endregion

        #region GetMethodTable
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref MethodTable GetMethodTableReference(object obj) => ref UnsafeCore.AsRef<MethodTable>(GetMethodTablePointer(obj));

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static MethodTable* GetMethodTablePointer(object obj) => (MethodTable*)UnsafeCore.Add(ref UnsafeCore.As<byte, nuint>(ref GetRawDataReference(obj)), -1);
        #endregion

        #region GetObjectHeader
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void* GetObjectHeaderPointer(object obj)
        {
            return UnsafeCore.AsPointer(ref UnsafeCore.Add(ref UnsafeCore.As<byte, nuint>(ref GetRawDataReference(obj)), -2));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static ref ObjectHeader GetObjectHeaderReference(object obj)
        {
            return ref UnsafeCore.As<nuint, ObjectHeader>(ref UnsafeCore.Add(ref UnsafeCore.As<byte, nuint>(ref GetRawDataReference(obj)), -2));
        }
        #endregion

        /// <summary>
        /// 获取对象数据的Span
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Span<T> GetObjectRawDataAsSpan<T>(object obj) where T : unmanaged
        {
            byte* first = GetRawDataPointer(obj);
            nuint size = (nuint)GetRawDataSize(obj) / (uint)sizeof(T);
            return new Span<T>(first, checked((int)size));
        }

        /// <summary>
        /// 获取对象的数据区域在堆中的大小
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static long GetRawDataSize(object obj)
        {
            [DoesNotReturn]
            static void Throw()
            {
                throw new ArgumentNullException(nameof(obj));
            }
            if (obj == null)
            {
                Throw();
            }
            ref MethodTable mt = ref GetMethodTableReference(obj);
            nuint rawSize = mt.BaseSize - (uint)(2 * sizeof(nuint));
            if ((mt.Flags >> 31) != 0)
            {
                rawSize += UnsafeCore.As<byte, uint>(ref GetRawDataReference(obj)) * (nuint)mt.ComponentSize;
            }
            return (nint)rawSize;
        }

        /// <summary>
        /// 获取对象的数据区域在堆中的大小
        /// </summary>
        /// <returns></returns>
        public static long GetRawDataSize<T>()
        {
            ref MethodTable mt = ref UnsafeCore.AsRef<MethodTable>((void*)typeof(T).TypeHandle.Value);
            bool empty = mt.BaseSize == 0;
            return mt.BaseSize - (2 * (uint)sizeof(nuint)) & -UnsafeCore.As<bool, byte>(ref empty); // 对于 Array 类型, 由于长度在实例中, 不计算数组成员所占大小
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static nuint GetRawObjectDataSize(object obj) // System.Runtime.CompilerServices.RuntimeHelpers.GetRawObjectDataSize
        {
            ref MethodTable mt = ref GetMethodTableReference(obj);
            nuint rawSize = mt.BaseSize - (uint)(2 * sizeof(nuint));
            if ((mt.Flags >> 31) != 0)
            {
                rawSize += UnsafeCore.As<byte, uint>(ref GetRawDataReference(obj)) * (nuint)mt.ComponentSize;
            }
            return rawSize;
        }

        /// <summary>
        /// 获取结构体大小
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetStructSize<T>() where T : struct => sizeof(T);

        /// <summary>
        /// 克隆一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T? Clone<T>(this T obj) where T : class
        {
            [DoesNotReturn]
            static void Throw()
            {
                throw new ArgumentNullException(nameof(obj));
            }
            if (obj == null)
            {
                Throw();
            }
            T? newObj = CloneEmptyObject(obj); //克隆对象
            if (newObj == null) return null;
            nuint size = (nuint)GetRawDataSize(obj); //长度
            byte* oldPtr = GetRawDataPointer(obj); //旧的地址
            byte* newPtr = GetRawDataPointer(newObj); //新的地址
            Buffer.MemoryCopy(oldPtr, newPtr, size, size);
            GC.KeepAlive(obj);
            return newObj;
        }

        /// <summary>
        /// 克隆至空的对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? CloneEmptyObject<T>(T obj) where T : class
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (AllocateUninitializedClone == null) return null;
            return (T)AllocateUninitializedClone(obj);
        }

        #region ToStringAddress
        /// <summary>
        /// 返回地址字符串
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToAddress(this nuint ptr)
        {
            //int size = 2 + sizeof(nint);
            //var str = new string('\0', 0);
            //fixed (char* p = str)
            //{
            //    p[0] = '0';
            //    p[1] = 'x';
            //    for (int i = 2; i < size; i++)
            //    {
                    
            //    }
            //}
            return "0x" + ((ulong)ptr).ToString("X").PadLeft(sizeof(UIntPtr) * 2, '0');
        }

        /// <summary>
        /// 返回地址字符串
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToAddress(this nint ptr)
        {
            return "0x" + ptr.ToString("X").PadLeft(sizeof(IntPtr) * 2, '0');
        }
        #endregion

        /// <summary>
        /// 修改对象类型(Handle)
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object ChangeObjectHandle(object obj, Type type) // 等会儿需要反汇编
        {
            void** p = (void**)GetPointer(obj);
            *p = (void*)type.TypeHandle.Value;
            return UnsafeCore.AsRef<object>(&p);
        }

        /// <summary>
        /// 修改对象类型(Handle)
        /// </summary>
        /// <param name="obj"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ChangeObjectHandle<T>(object obj) where T : class
        {
            *(IntPtr*)GetPointer(obj) = typeof(T).TypeHandle.Value;
            return UnsafeCore.As<T>(obj);
        }

        /// <summary>
        /// 申请一个对象,通过FreeObject释放
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static object AllocObject(Type type, nuint size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
#if NET6_0_OR_GREATER
            var p = NativeMemory.AllocZeroed(size);
            if (p == null) throw new OutOfMemoryException();
#else
            var p = Marshal.AllocHGlobal((nint)size);
            if (p == IntPtr.Zero) throw new OutOfMemoryException();
            Zero((void*)p, checked((nuint)size + (nuint)sizeof(IntPtr)));
#endif
            *(IntPtr*)p = type.TypeHandle.Value;
            //var obj = CoreUnsafe.Read<object>(&p);
            return *(object*)&p;
        }

        // TODO: nuint
        //public static object AllocObject(Type type, nint size)
        //{

        //}

        /// <summary>
        /// 申请一个对象,通过FreeObject释放
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static T AllocObject<T>(nuint size) where T : class
        {
            object obj = AllocObject(typeof(T), size);
            return UnsafeCore.As<object, T>(ref obj);
        }

        /// <summary>
        /// 申请一个对象,通过FreeObject释放
        /// </summary>
        /// <returns></returns>
        public static T AllocObject<T>() where T : class
        {
            var rawSize = GetRawDataSize<T>();
            if (rawSize < 0) rawSize = 0;
            var size = (uint)(rawSize + IntPtr.Size);
            return UnsafeCore.As<T>(AllocObject(typeof(T), (nuint)size));
        }

        /// <summary>
        /// 内存清0
        /// </summary>
        /// <param name="p"></param>
        /// <param name="size"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Zero(void* p, nuint size)
        {
            while (size > uint.MaxValue)
            {
                MemoryMarshal.CreateSpan<byte>(ref *(byte*)p, -1).Clear();
                size -= uint.MaxValue;
                p = (byte*)p + uint.MaxValue;
            }
            MemoryMarshal.CreateSpan<byte>(ref *(byte*)p, (int)size).Clear();
        }

        /// <summary>
        /// 释放AllocObject创建的对象
        /// </summary>
        /// <param name="obj"></param>
        public static void FreeObject(object obj)
        {
#if NET6_0_OR_GREATER
            NativeMemory.Free(GetPointer(obj));
#else
            Marshal.FreeHGlobal((nint)GetPointer(obj));
#endif
        }

        /// <summary>
        /// 通过AllocateUninitializedClone克隆出一个新的对象,不经过构造函数,由GC自动回收
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static unsafe object NewObject(Type type)
        {
            var data = (type.TypeHandle.Value, IntPtr.Zero, IntPtr.Zero); // handle data ptr
            data.Item3 = new IntPtr(&data);
            var clone = AllocateUninitializedClone(*(object*)&data.Item3);
            return clone;
        }
        /// <summary>
        /// 通过AllocateUninitializedClone克隆出一个新的对象,不经过构造函数,由GC自动回收
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static unsafe T NewObject<T>() where T : class
        {
            return UnsafeCore.As<T>(NewObject(typeof(T)));
        }

        /// <summary>
        /// 将字符串转换成 Span&lt;char&gt;
        /// </summary>
        /// <param name="str">字符串</param>
        /// <returns></returns>
        public static Span<char> AsSpan(string str)
        {
            var span = str.AsSpan();
            return MemoryMarshal.CreateSpan<char>(ref MemoryMarshal.GetReference(span), span.Length);
        }

        /// <summary>
        /// 比较两个对象的原始数据是否相等<br/>不比较类型
        /// </summary>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        /// <returns></returns>
        public static bool CompareRaw(object obj1, object obj2)
        {
            long obj1size = UnsafeHelper.GetRawDataSize(obj1);
            long obj2size = UnsafeHelper.GetRawDataSize(obj2);
            if (obj1size != obj2size) return false;

            ulong lenByte = (uint)obj1size;
            byte* r1 = GetRawDataPointer(obj1);
            byte* r2 = GetRawDataPointer(obj2);

            return true;
            // Call SpanHelpers.SequenceEqual
        }

        /// <summary>
        /// 将[多维]数组转换为Span
        /// </summary>
        /// <typeparam name="T">数组元素类型</typeparam>
        /// <param name="array">多维数组</param>
        /// <returns></returns>
        public static Span<T> AsSpan<T>(this Array array/*, int rank*/)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            int len = array.Length;
#if NET6_0_OR_GREATER
            var p = UnsafeCore.AsPointer(ref MemoryMarshal.GetArrayDataReference(array));
#else
            int rank = array.Rank;
            if (rank <= 1) rank = 0;
            byte* addr = GetRawDataPointer(array);
            int offset = rank * 8;
            // arrDataPtr + (Length/Padding) + rank
            var p = (byte*)addr + sizeof(nint) + offset;
#endif
            return new Span<T>(p, checked((int)(len * (long)GetArrayItemSize(array) / sizeof(T))));
        }

        /// <summary>
        /// 父类数据复制到子类
        /// </summary>
        /// <param name="parentObj">父类/基类</param>
        /// <param name="childObj">子类/派生类</param>
        /// <returns></returns>
        public static void CopyParentToChild<TParent, TChild>(TParent parentObj, TChild childObj)
            where TParent : class
            where TChild : class, TParent
        {
            if (parentObj == null) throw new ArgumentNullException(nameof(parentObj));
            if (childObj == null) throw new ArgumentNullException(nameof(childObj));

            byte* old = GetRawDataPointer(parentObj);
            byte* data = GetRawDataPointer(childObj);

            long len = GetRawDataSize(parentObj);

            Buffer.MemoryCopy((void*)old, (void*)data, (ulong)len, (ulong)len);
        }

        /// <summary>
        /// 子类数据复制到父类
        /// </summary>
        /// <param name="parentObj">父类/基类</param>
        /// <param name="childObj">子类/派生类</param>
        /// <returns></returns>
        public static void CopyChildToParent<TParent, TChild>(TChild childObj, TParent parentObj)
            where TParent : class
            where TChild : class, TParent
        {
            if (parentObj == null) throw new ArgumentNullException(nameof(parentObj));
            if (childObj == null) throw new ArgumentNullException(nameof(childObj));

            byte* old = GetRawDataPointer(childObj);
            byte* data = GetRawDataPointer(parentObj);

            long len = GetRawDataSize(parentObj);

            Buffer.MemoryCopy((void*)old, (void*)data, (ulong)len, (ulong)len);
        }

        /// <summary>
        /// 获取数组中每个元素占用的大小, 数组元素的大小不会超过65535字节
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static int GetArrayItemSize(Array array)
        {
            return *(ushort*)array.GetType().TypeHandle.Value;
        }

        /// <summary>
        /// 获取数组第0的值的指针
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* ToPointer<T>(this T[] str) // where T : unmanaged
        {
            return (T*)((byte*)GetPointer(str) + (sizeof(IntPtr) * 2));
        }

        /// <summary>
        /// 设置值
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="ptr"></param>
        /// <param name="value"></param>
        public static void SetValue<TValue>(nint ptr, TValue value) => *(TValue*)ptr = value;
        public static void SetValue<TValue>(void* ptr, TValue value) => *(TValue*)ptr = value;
        public static void SetValue<TFrom, TValue>(ref TFrom ptr, TValue value) => UnsafeCore.As<TFrom, TValue>(ref ptr) = value;

        /// <summary>
        /// 设置值<br/>
        /// 如果是class,自动设置它的RawData的值<br/>
        /// 如果是struct,自动设置它的值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValueAuto<TValue>(nint ptr, TValue value) => SetValueAuto((void*)ptr, value);

        /// <summary>
        /// 设置值<br/>
        /// 如果是class,自动设置它的RawData的值<br/>
        /// 如果是struct,自动设置它的值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValueAuto<TFrom, TValue>(ref TFrom ptr, TValue value) => SetValueAuto(GetPointer(ref ptr), value);

        /// <summary>
        /// 设置值<br/>
        /// 如果是class,自动设置它的RawData的值<br/>
        /// 如果是struct,自动设置它的值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValueAuto<TValue>(void* ptr, TValue value)
        {
            if(value is ValueType)
            {
                *(TValue*)ptr = value;
            }
            else
            {
                var data = GetRawDataPointer(value);
                var size = GetRawDataSize(value);
                Buffer.MemoryCopy(data, (void*)ptr, size, size);
            }
        }

#if NET7_0_OR_GREATER
        public static void Copy(void* source,void* destination, nint size) => NativeMemory.Copy(source, destination, checked((nuint)size));
        public static void Copy(void* source,void* destination, nuint size) => NativeMemory.Copy(source, destination, size);
#else
        public static void Copy(void* source,void* destination, nint size) => Buffer.MemoryCopy(source, destination, size, size);
        public static void Copy(void* source,void* destination, nuint size) => Buffer.MemoryCopy(source, destination, size, size);
#endif


        #region Field
        /// <summary>
        /// 获取实例字段
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static FieldInfo[] GetInstanceFields(Type type) => type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// 获取字段偏移
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">无法获取m_fieldHandle偏移地址</exception>
        public static int GetFieldOffset(Type type, string fieldName)
        {
            if (m_fieldHandle_offset < 0) throw new NotSupportedException();
            var fieldInfo = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo == null) return -1;
            byte* fieldInfoAddr = GetRawDataPointer(fieldInfo);
            IntPtr fieldHandle = *(IntPtr*)(fieldInfoAddr + m_fieldHandle_offset);
            return *(ushort*)(fieldHandle + sizeof(IntPtr) + 4);
        }

        /// <summary>
        /// 获取字段地址
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static byte* GetFieldPointer<T>(T obj, string fieldName) where T : class
        {
            if (obj == null) return null;
            int offset = GetFieldOffset(obj.GetType(), fieldName);
            if (offset == -1) return null;
            return GetRawDataPointer(obj) + offset;
        }

        //private static bool SetFieldValue<TValue>(nint addr, TValue value)
        //{
        //    if (addr == 0) return false;
        //    if (value is ValueType)
        //    {
        //        int size = UnsafeCore.SizeOf<TValue>();
        //        if (size <= 0) return false;
        //        byte* valueAddr = GetPointer(ref value);
        //        Buffer.MemoryCopy((void*)valueAddr, (void*)addr, size, size);
        //    }
        //    else
        //    {
        //        if (value == null)
        //        {
        //            *(IntPtr*)addr = IntPtr.Zero;
        //        }
        //        else
        //        {
        //            SetValue()
        //            nint val = GetPointer(value);
        //            *(IntPtr*)addr = val;
        //        }
        //    }
        //    return true;
        //}

        ///// <summary>
        ///// 设置Object字段的值
        ///// </summary>
        ///// <typeparam name="T"></typeparam>
        ///// <typeparam name="TValue"></typeparam>
        ///// <param name="obj"></param>
        ///// <param name="fieldName"></param>
        ///// <param name="value"></param>
        ///// <returns></returns>
        //public static bool SetObjectFieldValue<T, TValue>(T obj, string fieldName, TValue value) where T : class
        //{
        //    IntPtr addr = GetFieldPointer(obj, fieldName);
        //    if (addr == IntPtr.Zero) return false;
        //    return SetFieldValue(addr, value);
        //}

        ///// <summary>
        ///// 设置Struct字段的值
        ///// </summary>
        ///// <typeparam name="T"></typeparam>
        ///// <typeparam name="TValue"></typeparam>
        ///// <param name="obj"></param>
        ///// <param name="fieldName"></param>
        ///// <param name="value"></param>
        ///// <returns></returns>
        //public static bool SetStructFieldValue<T, TValue>(ref T obj, string fieldName, TValue value) where T : struct
        //{
        //    int offset = GetFieldOffset(typeof(T), fieldName);
        //    if (offset == -1) return false;
        //    byte* addr = GetPointer(ref obj) + offset;
        //    return SetFieldValue(addr, value);
        //}
        #endregion


    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
}