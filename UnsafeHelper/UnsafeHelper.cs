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
        public static T? CloneEmptyObject<T>(T? obj)
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
    }

}