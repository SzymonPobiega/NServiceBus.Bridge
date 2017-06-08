using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public class NativeSubscribeRouterTests
{
    [Test]
    public void Can_create_dynamic_type()
    {
        var router = new NativeSubscribeRouter(null, null);
        var type = router.GetType("MyNamespace.MyType, MyAssembly");

        Assert.AreEqual("MyAssembly", type.Assembly.GetName().Name);
        Assert.AreEqual("MyNamespace.MyType, MyAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", type.AssemblyQualifiedName);
    }

    [Test]
    public void Can_create_dynamic_type_for_a_nested_type()
    {
        var router = new NativeSubscribeRouter(null, null);
        var type = router.GetType("MyNamespace.MyType+NestedType, MyAssembly");

        Assert.AreEqual("MyNamespace.MyType+NestedType, MyAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", type.AssemblyQualifiedName);
    }

    class NestedType
    {
    }
}
