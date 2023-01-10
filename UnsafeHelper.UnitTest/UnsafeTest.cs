using IlyfairyLib.Unsafe;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
namespace UnsafeHelperTest
{
    public class UnsafeTest
    {
        [Fact]
        public unsafe void GetRefPointer()
        {
            int num = 1;
            void* p1 = (void*)UnsafeHelper.GetPointer(ref num);
            void* p2 = &num;
            Assert.True(p1 == p2);
        }

        [Fact]
        public unsafe void GetObjectPointer()
        {
            object obj = new object();
            var p1 = (void*)*(object**)Unsafe.AsPointer(ref obj);
            var p2 = UnsafeHelper.GetPointer(obj);
            Assert.True(p1 == p2);
        }

        [Fact]
        public void CopyParentToChild()
        {
            ParentClass parent = new();
            parent.A = "val1";
            ChildClass child = new();
            child.B = "val2";
            UnsafeHelper.CopyParentToChild(parent, child);
            Assert.True(parent.A == child.A);
            Assert.True(child.B == "val2");
        }

        [Fact]
        public void CopyChildToParent()
        {
            ChildClass child = new();
            child.A = "val1";
            ParentClass parent = new();
            UnsafeHelper.CopyChildToParent(child, parent);
            Assert.True(parent.A == child.A);
        }

        [Fact]
        public void ArrayElementSize()
        {
            var arr1 = new string[] { "str" };
            var arr2 = new decimal[] { 1m };
            Assert.True(UnsafeHelper.GetArrayItemSize(arr1) == IntPtr.Size);
            Assert.True(UnsafeHelper.GetArrayItemSize(arr2) == sizeof(decimal));
        }

        [Fact]
        public void StringAsSpan()
        {
            string qwq = " str"[1..];
            Assert.True(UnsafeHelper.AsSpan(qwq).SequenceEqual(qwq));
        }

        [Fact]
        public void ChangeObjectHandle()
        {
            string a = " str"[1..];
            Assert.True(UnsafeHelper.ChangeObjectHandle(a, typeof(long)).GetType() ==  typeof(long));
            Assert.True(UnsafeHelper.ChangeObjectHandle<object>(a).GetType() == typeof(object));
        }

        [Fact]
        public void AllocObject()
        {
            var obj = UnsafeHelper.AllocObject(typeof(long), (IntPtr)8);
            Assert.True(obj != null);
            var str = obj.ToString();
            Assert.True(str == "0");
            UnsafeHelper.FreeObject(obj);
        }

        [Fact]
        public void FieldOffset()
        {
            var offset = UnsafeHelper.GetFieldOffset(typeof(Foo), "C");
            Assert.True(offset == 8);
        }

        [Fact]
        public void ObjectSize()
        {
            Assert.True(UnsafeHelper.GetStructSize<Foo>() == 24);
            Assert.True(UnsafeHelper.GetRawDataSize(new int[] { 1, 2 }) == 16);
            Assert.True(UnsafeHelper.GetRawDataSize<ChildClass>() == IntPtr.Size * 2);
        }

        [Fact]
        public void ArrayAsSpan()
        {
            var arr = new int[10, 10, 10, 10, 2];
            ref int end = ref arr[9, 9, 9, 9, 1];
            end = 123;
            var span = UnsafeHelper.AsSpan<int>(arr);
            Assert.True(arr.Length == span.Length);
            Assert.True(span[^1] == end);
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    internal struct Foo //24字节
    {
        public int A; //offset:0
        public int B; //offset:4
        public long C; //offset:8
        public long D; //offset:16
    }
    internal class ParentClass //nint字节
    {
        public string A; //offset:0
    }
    internal class ChildClass : ParentClass //nint*2字节
    {
        public string B; //offset:nint
    }
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
