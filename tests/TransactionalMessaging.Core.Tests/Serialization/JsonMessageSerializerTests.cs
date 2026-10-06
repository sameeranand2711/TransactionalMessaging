using TransactionalMessaging.Core.Serialization;
using Xunit;

namespace TransactionalMessaging.Core.Tests.Serialization;

public class JsonMessageSerializerTests
{
    private readonly JsonMessageSerializer _serializer;

    public JsonMessageSerializerTests()
    {
        _serializer = new JsonMessageSerializer();
    }

    [Fact]
    public void ContentType_ReturnsJsonUtf8()
    {
        Assert.Equal("application/json; charset=utf-8", _serializer.ContentType);
    }

    [Fact]
    public void Serialize_SimpleObject_ReturnsBytes()
    {
        var obj = new TestMessage { Id = "123", Name = "Test" };

        var bytes = _serializer.Serialize(obj);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Deserialize_ValidBytes_ReturnsObject()
    {
        var original = new TestMessage { Id = "123", Name = "Test" };
        var bytes = _serializer.Serialize(original);

        var deserialized = _serializer.Deserialize<TestMessage>(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
    }

    [Fact]
    public void Serialize_NullMessage_ThrowsArgumentNullException()
    {
        TestMessage? nullMessage = null;

        Assert.Throws<ArgumentNullException>(() => _serializer.Serialize(nullMessage!));
    }

    [Fact]
    public void Deserialize_NullBytes_ThrowsArgumentNullException()
    {
        byte[]? nullBytes = null;

        Assert.Throws<ArgumentNullException>(() => _serializer.Deserialize<TestMessage>(nullBytes!));
    }

    [Fact]
    public void Serialize_Deserialize_PreservesProperties()
    {
        var original = new TestMessage
        {
            Id = "test-id",
            Name = "Test Message",
            Count = 42,
            Timestamp = DateTime.UtcNow
        };

        var bytes = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize<TestMessage>(bytes);

        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Count, deserialized.Count);
        Assert.Equal(original.Timestamp, deserialized.Timestamp);
    }

    private class TestMessage
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int Count { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
