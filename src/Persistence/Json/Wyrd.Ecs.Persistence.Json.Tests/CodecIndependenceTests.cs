using MemoryPack;

namespace Wyrd.Ecs.Persistence.Json.Tests;

[MemoryPackable]
[JsonPersistenceIgnore]
public partial struct BinaryOnlyComponent : IComponent
{
    public int Value;
}

public class CodecIndependenceTests : IDisposable
{
    private readonly string _jsonPath = Path.Combine(Path.GetTempPath(), $"wyrd-codec-independence-json-{Guid.NewGuid():N}.json");
    private readonly string _binaryPath = Path.Combine(Path.GetTempPath(), $"wyrd-codec-independence-binary-{Guid.NewGuid():N}.bin");

    public void Dispose()
    {
        if (File.Exists(_jsonPath)) File.Delete(_jsonPath);
        if (File.Exists(_binaryPath)) File.Delete(_binaryPath);
    }

    [Fact]
    public void AComponentRegisteredOnlyForBinary_IsAbsentFromAJsonSave()
    {
        var jsonRegistry = new ComponentCodecRegistry();
        JsonAutoRegistration.RegisterAll(jsonRegistry);

        var binaryRegistry = new ComponentCodecRegistry();
        binaryRegistry.Register<BinaryOnlyComponent>("BinaryOnlyComponent",
            v => MemoryPackSerializer.Serialize(v),
            bytes => MemoryPackSerializer.Deserialize<BinaryOnlyComponent>(bytes));

        jsonRegistry.TryGetByDiscriminator(typeof(BinaryOnlyComponent).FullName!, out _).Should().BeFalse();

        var source = new World();
        source.Commands.CreateEntity(new BinaryOnlyComponent { Value = 42 });
        source.ApplyCommands();

        WorldSnapshot.Save(source, jsonRegistry, new FileStore(_jsonPath));
        WorldSnapshot.Save(source, binaryRegistry, new FileStore(_binaryPath));

        var jsonTarget = new World();
        WorldSnapshot.Load(jsonTarget, jsonRegistry, new FileStore(_jsonPath));
        var binaryTarget = new World();
        WorldSnapshot.Load(binaryTarget, binaryRegistry, new FileStore(_binaryPath));

        var foundInJsonTarget = false;
        foreach (var _ in jsonTarget.Query<BinaryOnlyComponent>()) foundInJsonTarget = true;
        foundInJsonTarget.Should().BeFalse();

        var foundInBinaryTarget = false;
        foreach (var row in binaryTarget.Query<BinaryOnlyComponent>())
        {
            row.Get<BinaryOnlyComponent>().Value.Should().Be(42);
            foundInBinaryTarget = true;
        }
        foundInBinaryTarget.Should().BeTrue();
    }
}
