using System;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IlyfairyLib.Unsafe
{
    public static class UnsafeHelper
    {
        private static readonly Type RuntimeHelpersType;
        private static readonly Type BufferType;
        private static readonly Func<object?, object?> AllocateUninitializedClone;
        private static readonly Func<object?, UIntPtr> GetRawObjectDataSize;
        private static readonly GetRawDataDelegate GetRawData;
        private static readonly MemoryCopyDelegate Memmove;

        private delegate ref byte GetRawDataDelegate(object obj);
        private delegate void MemoryCopyDelegate(ref byte dest, ref byte src, UIntPtr len);

        static UnsafeHelper()
        {
            RuntimeHelpersType = typeof(RuntimeHelpers);
            BufferType = typeof(Buffer);
            AllocateUninitializedClone = RuntimeHelpersType.GetMethod("AllocateUninitializedClone", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<object, object>>()!;
            GetRawObjectDataSize = RuntimeHelpersType.GetMethod("GetRawObjectDataSize", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<object, UIntPtr>>()!;
            GetRawData = RuntimeHelpersType.GetMethod("GetRawData", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<GetRawDataDelegate>();
            Memmove = BufferType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(v => v.Name == "Memmove").FirstOrDefault((m) => m.GetGenericArguments().Length == 0).CreateDelegate<MemoryCopyDelegate>();
        }

        /// <summary>
        /// 获取对象原始数据大小
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static UIntPtr GetObjectRawDataSize(object obj)
        {
            return GetRawObjectDataSize(obj);
        }
        /// <summary>
        /// 获取对象地址的引用
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static ref byte GetObjectRawData(object obj)
        {
            return ref GetRawData(obj);
        }
        /// <summary>
        /// 获取对象地址
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe UIntPtr GetObjectRawDataAddress(object obj)
        {
            ref byte first = ref GetRawData(obj);
            fixed (void* p = &first)
            {
                return new UIntPtr(p);
            }
        }
        /// <summary>
        /// 获取对象数据的Span
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe Span<T> GetObjectRawDataAsSpan<T>(object obj) where T : unmanaged
        {
            ref byte first = ref GetRawData(obj);
            fixed (void* p = &first)
            {
                ulong size = (ulong)GetObjectRawDataSize(obj) / (ulong)sizeof(T);
                return new Span<T>(p, (int)size);
            }
        }
        /// <summary>
        /// 将src的内存复制到dest
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        /// <param name="len">复制的长度</param>
        public static void MemoryCopy(ref byte dest, ref byte src, UIntPtr len)
        {
            Memmove(ref dest, ref src, len);
        }
        /// <summary>
        /// 克隆一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T Clone<T>(T obj)
        {
            T newObj = UnsafeHelper.CloneEmptyObject(obj); //克隆对象
            UIntPtr size = UnsafeHelper.GetObjectRawDataSize(obj); //长度
            ref byte oldRef = ref UnsafeHelper.GetObjectRawData(obj); //旧的地址引用
            ref byte newRef = ref UnsafeHelper.GetObjectRawData(newObj); //新的地址引用
            MemoryCopy(ref newRef, ref oldRef, size);
            return newObj;
        }
        /// <summary>
        /// 克隆至空的对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T CloneEmptyObject<T>(T obj)
        {
            return (T?)AllocateUninitializedClone(obj);
        }
        /// <summary>
        /// 获取对象句柄(对象头)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe IntPtr GetObjectHandle(object obj)
        {
            ref byte objRawDataPtr = ref GetObjectRawData(obj);
            fixed (void* p = &objRawDataPtr)
            {
                return *(IntPtr*)(((byte*)p) - sizeof(IntPtr));
            }
        }
        /// <summary>
        /// 获取对象句柄
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static IntPtr GetObjectHandle(Type type)
        {
            return type.TypeHandle.Value;
        }
        /// <summary>
        /// 输出大写地址字符串, 不包含0x
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static unsafe string ToAddress(this UIntPtr p)
        {
            return p.ToString("X").PadLeft(sizeof(UIntPtr) * 2, '0');
        }
        /// <summary>
        /// 输出大写地址字符串, 不包含0x
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static unsafe string ToAddress(this IntPtr p)
        {
            return p.ToString("X").PadLeft(sizeof(UIntPtr) * 2, '0');
        }
        /// <summary>
        /// 修改对象Handle
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="handle"></param>
        public static unsafe void ChangeObjectHandle(object obj, IntPtr handle)
        {
            ref byte objRawDataPtr = ref GetObjectRawData(obj);
            fixed (byte* p = &objRawDataPtr)
            {
                var rawData = p - sizeof(IntPtr);
                *(IntPtr*)(rawData) = handle;
            }
        }
        /// <summary>
        /// 修改对象Handle
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        public static unsafe void ChangeObjectHandle(object obj, Type type)
        {
            ref byte objRawDataPtr = ref GetObjectRawData(obj);
            fixed (void* p = &objRawDataPtr)
            {
                var rawData = new IntPtr(p);
                rawData -= sizeof(IntPtr);
                *(IntPtr*)(rawData) = GetObjectHandle(type);
            }
        }
        /// <summary>
        /// 创建一个空的对象
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size">大小(包含Handle)</param>
        /// <returns></returns>
        public static unsafe object CreateEmptyObject(Type type, int size)
        {
            object[] obj = new object[1];
            IntPtr p = RuntimeHelpers.AllocateTypeAssociatedMemory(type, size);
            var span = GetObjectRawDataAsSpan<IntPtr>(obj);
            *((IntPtr*)p) = GetObjectHandle(type);
            span[1] = p;
            return obj[0];
        }
        /// <summary>
        /// 将字符串转换成 Span&lt;char&gt;
        /// </summary>
        /// <param name="text">字符串</param>
        /// <returns></returns>
        public static unsafe Span<char> AsSpanEx(this string text)
        {
            return new Span<char>((GetObjectRawDataAddress(text) + 4).ToPointer(), text.Length);
        }
        /// <summary>
        /// 比较两个对象的原始数据是否相等<br/>不比较类型
        /// </summary>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        /// <returns></returns>
        public static unsafe bool CompareRaw(object obj1, object obj2)
        {
            UIntPtr obj1size = UnsafeHelper.GetObjectRawDataSize(obj1);
            UIntPtr obj2size = UnsafeHelper.GetObjectRawDataSize(obj2);
            if (obj1size != obj2size) return false;

            ulong lenByte = (uint)obj1size;
            ref byte r1 = ref GetRawData(obj1);
            ref byte r2 = ref GetRawData(obj2);

            fixed (void* p1 = &r1, p2 = &r2)
            {
                if (lenByte % 8 == 0)
                {
                    var a = new Span<Int64>(p1, (int)(lenByte / 8));
                    var b = new Span<Int64>(p2, (int)(lenByte / 8));
                    return a.SequenceEqual(b);
                }
                else if (lenByte % 8 == 0)
                {
                    var a = new Span<Int32>(p1, (int)(lenByte / 4));
                    var b = new Span<Int32>(p2, (int)(lenByte / 4));
                    return a.SequenceEqual(b);
                }
                else if (lenByte % 8 == 0)
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
        }
        /// <summary>
        /// 将数组转换为Span
        /// </summary>
        /// <typeparam name="T">数组元素类型</typeparam>
        /// <param name="multiArray">多维数组</param>
        /// <param name="dimension">维度</param>
        /// <returns></returns>
        public static unsafe Span<T> AsSpan<T>(this Array multiArray, int dimension)
        {
            if (dimension <= 1) dimension = 0;
            ref byte a = ref GetObjectRawData(multiArray);
            fixed (byte* p = &a)
            {
                var len = multiArray.Length;
                var offset = dimension * 8;
                return new Span<T>(p + 8 + offset, len);
            }
        }
    }

}