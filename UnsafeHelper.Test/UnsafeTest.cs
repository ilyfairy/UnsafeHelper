using IlyfairyLib.Unsafe;
using System;
using System.Runtime.CompilerServices;
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
    }

    internal class ParentClass
    {
        public string A { get; set; }
    }
    internal class ChildClass : ParentClass
    {
        public string B { get; set; }
    }
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
