# UnsafeHelper
调用的.Net内部方法  
提供一些不安全方法  

#方法  
```cs
T CloneEmptyObject  //克隆出一个空的对象  
UIntPtr GetObjectDataSize  //获取对象数据占用大小  
UIntPtr GetObjectRawData  //获取对象地址
Span GetObjectAddress  //获取对象数据的Span
```
