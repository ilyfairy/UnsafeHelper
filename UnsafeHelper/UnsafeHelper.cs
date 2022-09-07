﻿using System;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace IlyfairyLib.Unsafe
{
    public static class UnsafeHelper
    {
        private static readonly Type RuntimeHelpersType;
        private static readonly Func<object?, object?> AllocateUninitializedClone;
        private static readonly Func<object?, UIntPtr> GetRawObjectDataSize;

        private delegate ref byte GetRawDataDelegate(object obj);

        static UnsafeHelper()
        {
            RuntimeHelpersType = typeof(RuntimeHelpers);
            AllocateUninitializedClone = RuntimeHelpersType.GetMethod("AllocateUninitializedClone", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<object, object>>()!;
            GetRawObjectDataSize = RuntimeHelpersType.GetMethod("GetRawObjectDataSize", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<object, UIntPtr>>()!;
        }

        /// <summary>
        /// 获取变量在栈上的地址
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="val"></param>
        /// <returns>address</returns>
        public static unsafe IntPtr GetVarAddress<T>(ref T val)
        {
            var r = __makeref(val);
            return *((IntPtr*)&r);
        }

        /// <summary>
        /// 获取引用对象在堆中的地址
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="val"></param>
        /// <returns>address</returns>
        public static unsafe IntPtr GetObjectAddress(object val)
        {
            var r = __makeref(val);
            return *(IntPtr*)*((IntPtr*)&r);
        }

        /// <summary>
        /// 获取对象数据区域的地址
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe IntPtr GetObjectRawDataAddress(object obj)
        {
            return GetObjectAddress(obj) + sizeof(IntPtr);
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
        public static UIntPtr GetObjectRawDataSize<T>(T obj)
        {
            return GetRawObjectDataSize(obj);
            //return System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        }

        /// <summary>
        /// 克隆一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe T? Clone<T>(this T obj) where T : class
        {
            T? newObj = CloneEmptyObject(obj); //克隆对象
            if (newObj == null) return null;
            UIntPtr size = GetObjectRawDataSize(obj); //长度
            IntPtr oldRef = GetObjectRawDataAddress(obj); //旧的地址引用
            IntPtr newRef = GetObjectRawDataAddress(newObj); //新的地址引用
            Buffer.MemoryCopy((void*)oldRef, (void*)newRef, (ulong)size, (ulong)size);
            return newObj;
        }

        /// <summary>
        /// 克隆至空的对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T? CloneEmptyObject<T>(T obj)
        {
            return (T?)AllocateUninitializedClone(obj)!;
        }

        /// <summary>
        /// 获取对象句柄(对象头)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static unsafe IntPtr GetObjectHandle(object obj)
        {
            IntPtr objRawDataPtr = GetObjectAddress(obj);
            return ((IntPtr*)objRawDataPtr)[0];
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
        public static unsafe void ChangeObjectHandle<T>(T obj, IntPtr handle) where T : class
        {
            IntPtr objRawDataPtr = GetObjectRawDataAddress(obj);
            var rawData = (byte*)objRawDataPtr - sizeof(IntPtr);
            *(IntPtr*)(rawData) = handle;
        }

        /// <summary>
        /// 修改对象Handle
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        public static unsafe void ChangeObjectHandle(object obj, Type type)
        {
            IntPtr objRawDataPtr = GetObjectAddress(obj);
            ((IntPtr*)objRawDataPtr)[0] = GetObjectHandle(type);
        }

        /// <summary>
        /// 创建一个空的对象<br/>创建的对象暂时不可释放
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
        public static unsafe Span<char> ToSpan(this string text)
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
            IntPtr r1 = GetObjectRawDataAddress(obj1);
            IntPtr r2 = GetObjectRawDataAddress(obj2);

            void* p1 = (void*)r1, p2 = (void*)r2;
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
            IntPtr a = GetObjectRawDataAddress(multiArray);
            var len = multiArray.Length;
            var offset = dimension * 8;
            return new Span<T>((byte*)a + 8 + offset, len);
        }

        /// <summary>
        /// 获取实例字段
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static FieldInfo[] GetInstanceFields(Type type) => type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// 父类数据复制到子类
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parentObj">父类/基类</param>
        /// <param name="childObj">子类/派生类</param>
        /// <returns></returns>
        public static unsafe void CopyToChild<TParent, TChild>(TParent parentObj, ref TChild childObj) where TParent : class where TChild : class
        {
            IntPtr old = GetObjectRawDataAddress(parentObj);
            IntPtr data = GetObjectRawDataAddress(childObj);

            var len = GetObjectRawDataSize(old);

            Buffer.MemoryCopy((void*)old, (void*)data, (ulong)len, (ulong)len);
        }

        public static unsafe IntPtr ToIntPtr(this string str)
        {
            return GetObjectAddress(str) + 4 + sizeof(IntPtr);
        }

        public static unsafe char* ToPointer(this string str)
        {
            return (char*)(GetObjectAddress(str) + 4 + sizeof(IntPtr)).ToPointer();
        }

        public static unsafe IntPtr ToIntPtr<T>(this T[] str) where T : unmanaged
        {
            return (GetObjectAddress(str) + sizeof(IntPtr) * 2);
        }

        public static unsafe T* ToPointer<T>(this T[] str) where T : unmanaged
        {
            return (T*)(GetObjectAddress(str) + sizeof(IntPtr) * 2).ToPointer();
        }

    }

}