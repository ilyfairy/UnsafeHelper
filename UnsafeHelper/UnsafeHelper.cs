using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
namespace IlyfairyLib.Unsafe
{
    /// <summary>
    /// Unsafe的工具方法
    /// </summary>
    public static class UnsafeHelper
    {
        private static readonly Type RuntimeHelpersType;
        private static readonly Func<object, object> AllocateUninitializedClone;
        private static readonly int m_fieldHandle_offset;

        unsafe static UnsafeHelper()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8605 // Unboxing a possibly null value.

            RuntimeHelpersType = typeof(RuntimeHelpers);
            AllocateUninitializedClone = (Func<object, object>)RuntimeHelpersType.GetMethod("AllocateUninitializedClone", BindingFlags.Static | BindingFlags.NonPublic).CreateDelegate(typeof(Func<object, object>));

            var info = typeof(UnsafeHelper).GetField("m_fieldHandle_offset", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            var m_fieldHandleInfo = info.GetType().GetField("m_fieldHandle", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var addr = (IntPtr*)GetObjectRawDataAddress(m_fieldHandleInfo);
            var m_fieldHandle = (IntPtr)m_fieldHandleInfo.GetValue(m_fieldHandleInfo);
            int size = (int)GetObjectRawDataSize(m_fieldHandleInfo);
            for (int i = 0; i < size; i += 1)
            {
                if (m_fieldHandle == addr[i])
                {
                    m_fieldHandle_offset = i * sizeof(IntPtr);
                    break;
                }
            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8605 // Unboxing a possibly null value.
        }

        /// <summary>
        /// 获取引用的地址
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="val"></param>
        /// <returns>address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr GetPointer<T>(ref T val) => (IntPtr)System.Runtime.CompilerServices.Unsafe.AsPointer(ref val);
        //{
        //    fixed(void* p = &val)
        //    {
        //        return (IntPtr)p;
        //    }
        //}

        /// <summary>
        /// 获取对象实例的地址
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr GetPointer(object obj) => (IntPtr)IL.GetPointer(obj);

        /// <summary>
        /// 获取对象数据区域的地址
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr GetObjectRawDataAddress(object obj)
        {
            return GetPointer(obj) + IntPtr.Size;
        }

        /// <summary>
        /// 获取对象数据的Span
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe Span<T> GetObjectRawDataAsSpan<T>(object obj) where T : unmanaged
        {
            IntPtr first = GetObjectRawDataAddress(obj);
            ulong size = (ulong)GetObjectRawDataSize(obj) / (ulong)sizeof(T);
            return new Span<T>((void*)first, (int)size);
        }

        /// <summary>
        /// 获取对象原始数据大小
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe long GetObjectRawDataSize<T>(this T obj) where T : class
        {
            if (obj == null) return -1;
            IntPtr objP = GetPointer(obj);
            IntPtr rawDataP = objP + sizeof(IntPtr);
            IntPtr objTable = *(IntPtr*)objP;
            long size = (*(uint*)(objTable + 4)) - (2 * 8);
            if((*(uint*)objTable & 2147483648U) > 0)
            {
                size += ((long)(*(ushort*)objTable) * (*(uint*)rawDataP));
            }
            if (size < 0) size = -1;
            //long size = *(uint*)(objTable + 4) - 2 * sizeof(IntPtr) + (*(ushort*)objTable * *(uint*)rawDataP);
            return size;
        }

        /// <summary>
        /// 获取对象原始数据大小, 如果获取失败返回-1
        /// </summary>
        /// <returns></returns>
        public static unsafe long GetObjectRawDataSize<T>() where T : class
        {
            IntPtr objTable = typeof(T).TypeHandle.Value;
            long size = (*(uint*)(objTable + 4)) - (2 * 8);
            if (size < 0) size = -1;
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
        public static int GetStructSize<T>() where T : struct => System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

        /// <summary>
        /// 克隆一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe T Clone<T>(this T obj) where T : class
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            T newObj = CloneEmptyObject(obj); //克隆对象
            long size = GetObjectRawDataSize(obj); //长度
            IntPtr oldRef = GetObjectRawDataAddress(obj); //旧的地址引用
            IntPtr newRef = GetObjectRawDataAddress(newObj); //新的地址引用
            Buffer.MemoryCopy((void*)oldRef, (void*)newRef, size, size);
            return newObj;
        }

        /// <summary>
        /// 克隆至空的对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CloneEmptyObject<T>(T obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            return (T)AllocateUninitializedClone(obj);
        }

        /// <summary>
        /// 获取对象句柄(对象头)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr GetObjectHandle(object obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            IntPtr objRawDataPtr = GetPointer(obj);
            return ((IntPtr*)objRawDataPtr)[0];
        }

        /// <summary>
        /// 返回地址字符串
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe string ToAddress(this UIntPtr p)
        {
            return "0x" + ((ulong)p).ToString("X").PadLeft(sizeof(UIntPtr) * 2, '0');
        }

        /// <summary>
        /// 返回地址字符串
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe string ToAddress(this IntPtr p)
        {
            return "0x" + p.ToString("X").PadLeft(sizeof(IntPtr) * 2, '0');
        }

        /// <summary>
        /// 修改对象类型(Handle)
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="handle"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe object ChangeObjectHandle(object obj, IntPtr handle)
        {
            *(IntPtr*)*(IntPtr*)&obj = handle;
            return obj;
        }

        /// <summary>
        /// 修改对象类型(Handle)
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe object ChangeObjectHandle(object obj, Type type)
        {
            *(IntPtr*)*(IntPtr*)&obj = type.TypeHandle.Value;
            return obj;
        }

        /// <summary>
        /// 修改对象类型(Handle)
        /// </summary>
        /// <param name="obj"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T ChangeObjectHandle<T>(object obj)
        {
            *(IntPtr*)*(IntPtr*)&obj = typeof(T).TypeHandle.Value;
            return (T)obj;
        }

        /// <summary>
        /// 申请一个对象,通过FreeObject释放
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static unsafe object AllocObject(Type type, IntPtr size)
        {
#if NET6_0_OR_GREATER
            IntPtr* p = (IntPtr*)NativeMemory.AllocZeroed(((UIntPtr)(ulong)size + sizeof(IntPtr)));
#else
            IntPtr* p = (IntPtr*)Marshal.AllocHGlobal((IntPtr)(size + sizeof(IntPtr)));
#endif
            p[0] = type.TypeHandle.Value;
            var obj = System.Runtime.CompilerServices.Unsafe.Read<object>(&p);
            return obj;
        }

        /// <summary>
        /// 申请一个对象,通过FreeObject释放
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static unsafe T AllocObject<T>(IntPtr size)
        {
#if NET6_0_OR_GREATER
            IntPtr* p = (IntPtr*)NativeMemory.AllocZeroed(((UIntPtr)(ulong)size + sizeof(IntPtr)));
#else
            IntPtr* p = (IntPtr*)Marshal.AllocHGlobal((IntPtr)(size + sizeof(IntPtr)));
#endif
            p[0] = typeof(T).TypeHandle.Value;
            var obj = System.Runtime.CompilerServices.Unsafe.Read<T>(&p);
            return obj;
        }

        /// <summary>
        /// 申请一个对象,通过FreeObject释放
        /// </summary>
        /// <returns></returns>
        public static unsafe T AllocObject<T>() where T : class
        {
            IntPtr size = (IntPtr)(GetObjectRawDataSize<T>() + sizeof(IntPtr));
#if NET6_0_OR_GREATER
            IntPtr* p = (IntPtr*)NativeMemory.AllocZeroed(((UIntPtr)(ulong)size + sizeof(IntPtr)));
#else
            IntPtr* p = (IntPtr*)Marshal.AllocHGlobal((IntPtr)(size + sizeof(IntPtr)));
#endif
            p[0] = typeof(T).TypeHandle.Value;
            var obj = System.Runtime.CompilerServices.Unsafe.Read<T>(&p);
            return obj;
        }

        /// <summary>
        /// 释放AllocObject创建的对象
        /// </summary>
        /// <param name="obj"></param>
        public static unsafe void FreeObject(object obj)
        {
            //NativeMemory.Free((void*)GetObjectAddress(obj));
            Marshal.FreeHGlobal((IntPtr)GetPointer(obj));
        }

        /// <summary>
        /// 将字符串转换成 Span&lt;char&gt;
        /// </summary>
        /// <param name="str">字符串</param>
        /// <returns></returns>
        public static unsafe Span<char> AsSpan(string str)
        {
            fixed(char* p = str)
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
        public static unsafe bool CompareRaw(object obj1, object obj2)
        {
            long obj1size = UnsafeHelper.GetObjectRawDataSize(obj1);
            long obj2size = UnsafeHelper.GetObjectRawDataSize(obj2);
            if (obj1size != obj2size) return false;

            ulong lenByte = (uint)obj1size;
            IntPtr r1 = GetObjectRawDataAddress(obj1);
            IntPtr r2 = GetObjectRawDataAddress(obj2);

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
        /// <param name="multiArray">多维数组</param>
        /// <param name="dimension">维度</param>
        /// <returns></returns>
        public static unsafe Span<T> AsSpan<T>(this Array multiArray, int dimension)
        {
            if (dimension <= 1) dimension = 0;
            IntPtr a = GetObjectRawDataAddress(multiArray);
            int len = multiArray.Length;
            int offset = dimension * 8;
            return new Span<T>((byte*)a + 8 + offset, len);
        }

        /// <summary>
        /// 父类数据复制到子类
        /// </summary>
        /// <param name="parentObj">父类/基类</param>
        /// <param name="childObj">子类/派生类</param>
        /// <returns></returns>
        public static unsafe void CopyParentToChild<TParent, TChild>(TParent parentObj, TChild childObj) 
            where TParent : class 
            where TChild : class , TParent
        {
            if (parentObj == null) throw new ArgumentNullException(nameof(parentObj));
            if (childObj == null) throw new ArgumentNullException(nameof(childObj));

            IntPtr old = GetObjectRawDataAddress(parentObj);
            IntPtr data = GetObjectRawDataAddress(childObj);

            long len = GetObjectRawDataSize(parentObj);

            Buffer.MemoryCopy((void*)old, (void*)data, (ulong)len, (ulong)len);
        }

        /// <summary>
        /// 子类数据复制到父类
        /// </summary>
        /// <param name="parentObj">父类/基类</param>
        /// <param name="childObj">子类/派生类</param>
        /// <returns></returns>
        public static unsafe void CopyChildToParent<TParent, TChild>(TChild childObj, TParent parentObj)
            where TParent : class
            where TChild : class, TParent
        {
            if (parentObj == null) throw new ArgumentNullException(nameof(parentObj));
            if (childObj == null) throw new ArgumentNullException(nameof(childObj));

            IntPtr old = GetObjectRawDataAddress(childObj);
            IntPtr data = GetObjectRawDataAddress(parentObj);

            long len = GetObjectRawDataSize(parentObj);

            Buffer.MemoryCopy((void*)old, (void*)data, (ulong)len, (ulong)len);
        }

        /// <summary>
        /// 获取数组中每个元素占用的大小, 数组元素的大小不会超过65535字节
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static unsafe int GetArrayItemSize(Array array)
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
        public static unsafe T* ToPointer<T>(this T[] str) // where T : unmanaged
        {
            return (T*)(GetPointer(str) + sizeof(IntPtr) * 2).ToPointer();
        }

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
        public static unsafe int GetFieldOffset(Type type, string fieldName)
        {
            var fieldInfo = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo == null) return -1;
            IntPtr fieldInfoAddr = GetObjectRawDataAddress(fieldInfo);
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
        public static unsafe IntPtr GetFieldAddress<T>(T obj, string fieldName) where T : class
        {
            if (obj == null) return IntPtr.Zero;
            int offset = GetFieldOffset(obj.GetType(), fieldName);
            if (offset == -1) return IntPtr.Zero;
            return GetObjectRawDataAddress(obj) + offset;
        }

        private static unsafe bool SetFieldValue<TValue>(IntPtr addr, TValue value)
        {
            if (addr == IntPtr.Zero) return false;
            if (value is ValueType)
            {
                int size = System.Runtime.CompilerServices.Unsafe.SizeOf<TValue>();
                if (size <= 0) return false;
                IntPtr valueAddr = GetPointer(ref value);
                Buffer.MemoryCopy((void*)valueAddr, (void*)addr, size, size);
            }
            else
            {
                if (value == null)
                {
                    *(IntPtr*)addr = IntPtr.Zero;
                }
                else
                {
                    IntPtr val = GetPointer(value);
                    *(IntPtr*)addr = val;
                }
            }
            return true;
        }

        /// <summary>
        /// 设置Object字段的值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="obj"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static unsafe bool SetObjectFieldValue<T, TValue>(T obj, string fieldName, TValue value) where T : class
        {
            IntPtr addr = GetFieldAddress(obj, fieldName);
            return SetFieldValue(addr, value);
        }

        /// <summary>
        /// 设置Struct字段的值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="obj"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static unsafe bool SetStructFieldValue<T, TValue>(ref T obj, string fieldName, TValue value) where T : struct
        {
            int offset = GetFieldOffset(typeof(T), fieldName);
            IntPtr addr = GetPointer(ref obj) + offset;
            return SetFieldValue(addr, value);
        }
        #endregion


    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
}