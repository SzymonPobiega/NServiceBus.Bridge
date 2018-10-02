using MyNamespace;
using NUnit.Framework;

[TestFixture]
public class RuntimeTypeGeneratorTests
{
    [Test]
    public void Can_create_dynamic_type()
    {
        var router = new RuntimeTypeGenerator();
        var type = router.GetType("MyNamespace.MyType, MyAssembly");

        Assert.AreEqual("MyAssembly", type.Assembly.GetName().Name);
        Assert.AreEqual("MyNamespace.MyType, MyAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", type.AssemblyQualifiedName);
    }

    [Test]
    public void Can_create_dynamic_type_for_a_nested_type()
    {
        var router = new RuntimeTypeGenerator();
        var type = router.GetType("MyNamespace.MyType+NestedType, MyAssembly");

        Assert.AreEqual("MyNamespace.MyType+NestedType, MyAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", type.AssemblyQualifiedName);
    }

    [Test]
    public void Returns_a_known_type()
    {
        var router = new RuntimeTypeGenerator();
        var type = router.GetType("MyNamespace.MyKnownType, NServiceBus.Bridge.Tests.Helpers");

        Assert.AreSame(type, typeof(MyKnownType));
    }

    [Test]
    public void Returns_a_known_type_even_if_version_does_not_match()
    {
        var router = new RuntimeTypeGenerator();
        var type = router.GetType("MyNamespace.MyKnownType, NServiceBus.Bridge.Tests.Helpers, Version=1.2.3.4");

        Assert.AreSame(type, typeof(MyKnownType));
    }
}



