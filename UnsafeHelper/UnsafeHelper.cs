#define HAS_UNSAFE
#define HAS_SPAN
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if NULLABLE
#nullable enable
#endif

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
        }

        /// <summary>
        /// 获取变量在栈上的地址
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="val"></param>
        /// <returns>address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr GetVarAddress<T>(ref T val)
        {
            var r = __makeref(val);
            return *((IntPtr*)&r);
        }

        /// <summary>
        /// 获取引用对象在堆中的地址
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr GetObjectAddress(object obj)
        {
            var r = __makeref(obj);
            return *(IntPtr*)*(IntPtr*)&r;
        }

        /// <summary>
        /// 获取对象数据区域的地址
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr GetObjectRawDataAddress(object obj)
        {
            var r = __makeref(obj);
            return *(IntPtr*)*(IntPtr*)&r + sizeof(IntPtr);
        }

#if HAS_SPAN
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
#endif


        /// <summary>
        /// 获取对象原始数据大小
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe long GetObjectRawDataSize<T>(this T obj) where T : class
        {
            if (obj == null) return -1;
            IntPtr objP = GetObjectAddress(obj);
            IntPtr rawDataP = objP + sizeof(IntPtr);
            IntPtr objTable = *(IntPtr*)objP;
            long size = *(uint*)(objTable + 4) - 2 * sizeof(IntPtr) + (*(ushort*)objTable * *(uint*)rawDataP);
            return size;
        }

        /// <summary>
        /// 获取对象原始数据大小
        /// </summary>
        /// <returns></returns>
        public static unsafe long GetObjectRawDataSize<T>() where T : class
        {
            IntPtr objTable = typeof(T).TypeHandle.Value;
            long size = *(uint*)(objTable + 4) - 2 * sizeof(IntPtr);
            return size;
        }

#if HAS_UNSAFE
        /// <summary>
        /// 获取结构体大小
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetStructSize<T>() where T : struct => System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
#endif

        //net Framework没有AllocateUninitializedClone
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
            Buffer.MemoryCopy((void*)oldRef, (void*)newRef, (ulong)size, (ulong)size);
            return newObj;
        }

        //net Framework没有AllocateUninitializedClone
        /// <summary>
        /// 克隆至空的对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CloneEmptyObject<T>(T obj)
        {
            if(obj == null) throw new ArgumentNullException(nameof(obj));
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
            if(obj == null) throw new ArgumentNullException(nameof(obj));
            IntPtr objRawDataPtr = GetObjectAddress(obj);
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
        public static unsafe void ChangeObjectHandle(object obj, IntPtr handle)
        {
            IntPtr objRawDataPtr = GetObjectRawDataAddress(obj);
            var rawData = (byte*)objRawDataPtr - sizeof(IntPtr);
            *(IntPtr*)(rawData) = handle;
        }

        /// <summary>
        /// 修改对象类型(Handle)
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ChangeObjectHandle(object obj, Type type)
        {
            IntPtr objRawDataPtr = GetObjectAddress(obj);
            ((IntPtr*)objRawDataPtr)[0] = type.TypeHandle.Value;
        }

        /// <summary>
        /// 修改对象类型(Handle)
        /// </summary>
        /// <param name="obj"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T ChangeObjectHandle<T>(object obj)
        {
            IntPtr objRawDataPtr = GetObjectAddress(obj);
            ((IntPtr*)objRawDataPtr)[0] = typeof(T).TypeHandle.Value;
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
            Marshal.FreeHGlobal((IntPtr)GetObjectAddress(obj));
        }

#if HAS_SPAN
        /// <summary>
        /// 将字符串转换成 Span&lt;char&gt;
        /// </summary>
        /// <param name="text">字符串</param>
        /// <returns></returns>
        public static unsafe Span<char> ToSpan(this string text)
        {
            return new Span<char>((GetObjectRawDataAddress(text) + 4).ToPointer(), text.Length);
        }
#endif

#if HAS_SPAN
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
#endif

#if HAS_SPAN
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
#endif

        /// <summary>
        /// 父类数据复制到子类
        /// </summary>
        /// <param name="parentObj">父类/基类</param>
        /// <param name="childObj">子类/派生类</param>
        /// <returns></returns>
        public static unsafe void CopyToChild<TParent, TChild>(TParent parentObj, ref TChild childObj) where TParent : class where TChild : class
        {
            if (parentObj == null) throw new ArgumentNullException(nameof(parentObj));
            if (childObj == null) throw new ArgumentNullException(nameof(childObj));

            IntPtr old = GetObjectRawDataAddress(parentObj);
            IntPtr data = GetObjectRawDataAddress(childObj);

            long len = GetObjectRawDataSize(parentObj);

            Buffer.MemoryCopy((void*)old, (void*)data, (ulong)len, (ulong)len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr ToIntPtr(this string str)
        {
            return GetObjectAddress(str) + 4 + sizeof(IntPtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe char* ToPointer(this string str)
        {
            return (char*)(GetObjectAddress(str) + 4 + sizeof(IntPtr)).ToPointer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr ToIntPtr<T>(this T[] str) where T : unmanaged
        {
            return (GetObjectAddress(str) + sizeof(IntPtr) * 2);
        }

        /// <summary>
        /// 获取数组第0的值的指针
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* ToPointer<T>(this T[] str) where T : unmanaged
        {
            return (T*)(GetObjectAddress(str) + sizeof(IntPtr) * 2).ToPointer();
        }

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
                IntPtr valueAddr = GetVarAddress(ref value);
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
                    IntPtr val = GetObjectAddress(value);
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
            IntPtr addr = GetVarAddress(ref obj) + offset;
            return SetFieldValue(addr, value);
        }

    }

    /// <summary>
    /// 类型句柄信息 <see cref="System.Runtime.CompilerServices.MethodTable"/>
    /// </summary>
    public unsafe ref struct TypeHandleTable
    {
        public static TypeHandleTable* From<T>()
        {
            return (TypeHandleTable*)typeof(T).TypeHandle.Value;
        }
        public static TypeHandleTable* From(Type type)
        {
            return (TypeHandleTable*)type.TypeHandle.Value;
        }
        public static TypeHandleTable* From(IntPtr typeHandle)
        {
            return (TypeHandleTable*)typeHandle;
        }

        /// <summary>
        /// 判断是否为值类型
        /// </summary>
        public bool IsValueType
        {
            get
            {
                fixed(TypeHandleTable* p = &this)
                {
                    return (*(uint*)p & 0b11000000000000000000U) == 0b1000000000000000000U;
                }
            }
        }



    }
}