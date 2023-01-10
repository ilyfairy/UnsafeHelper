using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using IlyfairyLib.Unsafe.Internal;
using UnsafeCore = System.Runtime.CompilerServices.Unsafe;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
namespace IlyfairyLib.Unsafe
{
    /// <summary>
    /// Unsafe的工具方法
    /// </summary>
    public static unsafe class UnsafeHelper
    {
        private static readonly Type RuntimeHelpersType;
        private static readonly Func<object, object>? AllocateUninitializedClone;
        private static readonly int m_fieldHandle_offset = -1; // RtFieldInfo中的m_fieldHandle的偏移地址

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
        public static byte* GetPointer(object obj) => (byte*)IL.GetPointer(obj);
        #endregion

        #region GetRawData
        /// <summary>
        /// 获取对象数据区域的地址
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetRawDataReference(object obj) => ref (*((byte*)GetPointer(obj) + IntPtr.Size));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* GetRawDataPointer(object obj) => (byte*)GetPointer(obj) + sizeof(nint);
        #endregion

        #region GetMethodTable
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref MethodTable GetMethodTableReference(object obj) => ref UnsafeCore.As<byte,MethodTable>(ref IL.GetMethodTableReference(obj));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodTable* GetMethodTable(object obj) => (MethodTable*)IL.GetMethodTable(obj);
        #endregion

        #region GetObjectHeader

        #endregion

        /// <summary>
        /// 获取对象数据的Span
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Span<T> GetObjectRawDataAsSpan<T>(object obj) where T : unmanaged
        {
            byte* first = GetRawDataPointer(obj);
            ulong size = (ulong)GetRawDataSize(obj) / (ulong)sizeof(T);
            return new Span<T>((void*)first, checked((int)size));
        }

        /// <summary>
        /// 获取对象的数据区域在堆中的大小
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static long GetRawDataSize(object obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            byte* objptr = GetPointer(obj);
            byte* rawDataPtr = objptr + sizeof(nint);
            nint objTable = *(nint*)objptr;
            long size = (*(uint*)(objTable + 4)) - (2 * sizeof(nint));
            if ((*(uint*)objTable & 2147483648U) > 0)
            {
                size += ((long)(*(ushort*)objTable) * (*(uint*)rawDataPtr));
            }
            if (size < 0) size = -1;
            //long size = *(uint*)(objTable + 4) - 2 * sizeof(IntPtr) + (*(ushort*)objTable * *(uint*)rawDataP);
            return size;
        }

        /// <summary>
        /// 获取对象的数据区域在堆中的大小
        /// </summary>
        /// <returns></returns>
        public static long GetRawDataSize<T>()
        {
            nint methodTable = typeof(T).TypeHandle.Value;    
            long size = (*(uint*)(methodTable + 4)) - (2 * sizeof(nint));
            if (size < 0) size = 0;
            //long size = *(uint*)(objTable + 4) - 2 * sizeof(IntPtr) + (*(ushort*)objTable * *(uint*)rawDataP);
            return size;
            //IntPtr objTable = typeof(T).TypeHandle.Value;
            //long size = *(uint*)(objTable + 4) - 2 * sizeof(IntPtr);
            //if (size < 0) size = -1;
            //return size;
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
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            T? newObj = CloneEmptyObject(obj); //克隆对象
            if (newObj == null) return null;
            long size = GetRawDataSize(obj); //长度
            byte* oldPtr = GetRawDataPointer(obj); //旧的地址
            byte* newPtr = GetRawDataPointer(newObj); //新的地址
            Buffer.MemoryCopy((void*)oldPtr, (void*)newPtr, size, size);
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
        public static object ChangeObjectHandle(object obj, Type type)
        {
            *(IntPtr*)GetPointer(obj) = type.TypeHandle.Value;
            return obj;
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
        public static object AllocObject(Type type, nint size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
#if NET6_0_OR_GREATER
            var p = (IntPtr)NativeMemory.AllocZeroed(((UIntPtr)(ulong)size + sizeof(IntPtr)));
            if (p == IntPtr.Zero) throw new OutOfMemoryException();
#else
            var p = (IntPtr)Marshal.AllocHGlobal((size + sizeof(IntPtr)));
            if (p == IntPtr.Zero) throw new OutOfMemoryException();
            Zero((void*)p, checked((nuint)size + (nuint)sizeof(IntPtr)));
#endif
            *(IntPtr*)p = type.TypeHandle.Value;
            //var obj = CoreUnsafe.Read<object>(&p);
            var obj = IL.As<object>(p);
            return obj;
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
        public static T AllocObject<T>(IntPtr size) where T : class
        {
            return UnsafeCore.As<T>(AllocObject(typeof(T), size));
        }

        /// <summary>
        /// 申请一个对象,通过FreeObject释放
        /// </summary>
        /// <returns></returns>
        public static T AllocObject<T>() where T : class
        {
            var rawSize = GetRawDataSize<T>();
            if (rawSize < 0) rawSize = 0;
            var size = (IntPtr)(rawSize + IntPtr.Size);
            return UnsafeCore.As<T>(AllocObject(typeof(T), size));
        }

        /// <summary>
        /// 内存清0
        /// </summary>
        /// <param name="p"></param>
        /// <param name="size"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Zero(void* p, nuint size)
        {
            //if ((void*)size < (void*)0) throw new ArgumentOutOfRangeException(nameof(size));
            while (size > (nuint)int.MaxValue)
            {
                new Span<byte>(p, int.MaxValue).Clear();
                size -= int.MaxValue;
                p = (byte*)p + int.MaxValue;
            }
            new Span<byte>(p, (int)size).Clear();
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
            var clone = AllocateUninitializedClone(IL.As<object>(data.Item3));
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
            fixed (char* p = str)
            {
                return new Span<char>(p, str.Length);
            }
            //return new Span<char>((GetObjectRawDataAddress(text) + 4).ToPointer(), text.Length);
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

            void* p1 = (void*)r1, p2 = (void*)r2;
            if (lenByte % 8 == 0)
            {
                var a = new Span<Int64>(p1, (int)(lenByte / 8));
                var b = new Span<Int64>(p2, (int)(lenByte / 8));
                return a.SequenceEqual(b);
            }
            else if (lenByte % 4 == 0)
            {
                var a = new Span<Int32>(p1, (int)(lenByte / 4));
                var b = new Span<Int32>(p2, (int)(lenByte / 4));
                return a.SequenceEqual(b);
            }
            else if (lenByte % 2 == 0)
            {
                var a = new Span<Int16>(p1, (int)(lenByte / 2));
                var b = new Span<Int16>(p2, (int)(lenByte / 2));
                return a.SequenceEqual(b);
            }
            else
            {
                var a = new Span<Byte>(p1, (int)lenByte);
                var b = new Span<Byte>(p2, (int)lenByte);
                return a.SequenceEqual(b);
            }
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
            return (T*)(GetPointer(str) + (sizeof(IntPtr) * 2));
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
        public static void Copy(void* source,void* destination, nint size) => NativeMemory.Copy(source, destination, size, size);
        public static void Copy(void* source,void* destination, nuint size) => NativeMemory.Copy(source, destination, size, size);
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