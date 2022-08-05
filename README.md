# UnsafeHelper
调用的.Net内部方法  
提供一些不安全方法  

# 方法  
```csharp
T CloneEmptyObject();  //克隆出一个空的对象
UIntPtr GetObjectDataSize();  //获取对象数据占用大小
UIntPtr GetObjectRawData();  //获取对象地址
Span<T> GetObjectAddress();  //获取对象数据的Span
void MemoryCopy();  //内存复制
T Clone();  //克隆对象
```
