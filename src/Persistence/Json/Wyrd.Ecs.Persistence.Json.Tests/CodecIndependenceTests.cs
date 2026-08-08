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
        var jsonRegistry = new CodecRegistry();
        JsonAutoRegistration.RegisterAll(jsonRegistry);

        var binaryRegistry = new CodecRegistry();
        binaryRegistry.Register<BinaryOnlyComponent>("BinaryOnlyComponent",
            v => MemoryPackSerializer.Serialize(v),
            bytes => MemoryPackSerializer.Deserialize<BinaryOnlyComponent>(bytes));

        jsonRegistry.TryGetByDiscriminator(typeof(BinaryOnlyComponent).FullName!, out _).Should().BeFalse();

        var source = new World();
        source.Commands.CreateEntity(new BinaryOnlyComponent { Value = 42 });
        source.ApplyCommands();

        source.DefaultCodecRegistry = jsonRegistry;
        source.Save(_jsonPath);
        source.DefaultCodecRegistry = binaryRegistry;
        source.Save(_binaryPath);

        var jsonTarget = new World();
        jsonTarget.DefaultCodecRegistry = jsonRegistry;
        jsonTarget.Load(_jsonPath);
        var binaryTarget = new World();
        binaryTarget.DefaultCodecRegistry = binaryRegistry;
        binaryTarget.Load(_binaryPath);

        var foundInJsonTarget = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<BinaryOnlyComponent>>().Resolve(jsonTarget))
            if (chunk.Count > 0) foundInJsonTarget = true;
        foundInJsonTarget.Should().BeFalse();

        var foundInBinaryTarget = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<BinaryOnlyComponent>>().Resolve(binaryTarget))
        {
            var values = chunk.Access<Ref<BinaryOnlyComponent>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                values[i].Value.Should().Be(42);
                foundInBinaryTarget = true;
            }
        }
        foundInBinaryTarget.Should().BeTrue();
    }
}
