using System.Reflection;
using System.Runtime.CompilerServices;

namespace IlyfairyLib.Unsafe
{
    public static class UnsafeHelper
    {
        private static readonly Type RuntimeHelpersType;
        private static readonly Func<object?, object?> AllocateUninitializedClone;
        private static readonly Func<object?, UIntPtr> GetRawObjectDataSize;
        private static readonly GetRawDataDelegate GetRawData;

        private delegate ref byte GetRawDataDelegate(object obj);
        private delegate void MemoryCopyDelegate(ref byte dest, ref byte src, UIntPtr len);

        static UnsafeHelper()
        {
            RuntimeHelpersType = typeof(RuntimeHelpers);
            AllocateUninitializedClone = RuntimeHelpersType.GetMethod("AllocateUninitializedClone", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<object, object>>()!;
            GetRawObjectDataSize = RuntimeHelpersType.GetMethod("GetRawObjectDataSize", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<object, UIntPtr>>()!;
            GetRawData = RuntimeHelpersType.GetMethod("GetRawData", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<GetRawDataDelegate>();
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
        /// 获取对象大小
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static UIntPtr GetObjectDataSize(object obj)
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
        public static unsafe UIntPtr GetObjectAddress(object obj)
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
        public static unsafe Span<byte> GetObjectRawDataAsSpan(object obj)
        {
            ref byte first = ref GetRawData(obj);
            fixed (void* p = &first)
            {
                return new Span<byte>(p, (int)GetObjectDataSize(obj));
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
            var methods = typeof(Buffer).GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo[]? r = methods.Where(v => v.Name == "Memmove").ToArray();

            var method = r.FirstOrDefault((m) => m.GetGenericArguments().Length == 0);
            var func = method.CreateDelegate<MemoryCopyDelegate>();
            func(ref dest, ref src, len);
        }
        /// <summary>
        /// 克隆一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T Clone<T>(T obj)
        {
            var newObj = UnsafeHelper.CloneEmptyObject(obj); //克隆对象
            var size = UnsafeHelper.GetObjectDataSize(obj); //长度
            ref byte oldRef = ref UnsafeHelper.GetObjectRawData(obj); //旧的地址引用
            ref byte newRef = ref UnsafeHelper.GetObjectRawData(newObj); //新的地址引用
            MemoryCopy(ref newRef, ref oldRef, size);
            return newObj;
        }
    }

}