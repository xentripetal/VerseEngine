namespace Verse.ECS;

public class ArchetypeRegistry
{
	private Archetype?[] _archetypes = new Archetype?[1024];
	private readonly Dictionary<EcsID, Archetype> _byHashId = new Dictionary<EcsID, Archetype>();
	public event Action<World, Archetype> OnArchetypeCreated, OnArchetypeRemoved;

	public void Add(Archetype archetype)
	{
		var idx = (int)archetype.Generation;
		if (_archetypes.Length <= idx) {
			EcsAssert.Assert(_archetypes.Length == idx, "out of order archetype registered");
			var newSize = idx + 1024;
			Array.Resize(ref _archetypes, newSize);
		}
		if (_archetypes[idx] == null) {
			_archetypes[idx] = archetype;
			_byHashId[archetype.HashId] = archetype;
			OnArchetypeCreated?.Invoke(archetype.World, archetype);
		} else {
			EcsAssert.Panic(false, $"Archetype with generation {archetype.Generation} already exists in the registry.");
		}
	}

	public void Remove(Archetype archetype)
	{
		var idx = (int)archetype.Generation;
		if (idx < 0 || idx >= _archetypes.Length) {
			EcsAssert.Panic(false, $"Archetype with generation {archetype.Generation} is out of bounds.");
		}
		if (_archetypes[idx] == null) {
			EcsAssert.Panic(false, $"Archetype with generation {archetype.Generation} does not exist in the registry.");
		}
		_archetypes[idx] = null;
		_byHashId.Remove(archetype.HashId);
		OnArchetypeRemoved?.Invoke(archetype.World, archetype);
	}

	public Archetype GetFromHashId(EcsID hashId)
	{
		if (_byHashId.TryGetValue(hashId, out var archetype)) {
			return archetype;
		}
		EcsAssert.Panic(false, $"Archetype with hash ID {hashId} does not exist in the registry.");
		return default; // This line will never be reached due to the panic above.
	}

	public bool TryGetFromHashId(EcsID hashId, out Archetype? archetype)
	{
		return _byHashId.TryGetValue(hashId, out archetype);
	}

	public Archetype? GetFromGeneration(ulong generation)
	{
		var idx = (int)generation;
		if (idx < 0 || idx >= _archetypes.Length) {
			EcsAssert.Panic(false, $"Generation {generation} is out of bounds.");
		}
		return _archetypes[idx];
	}

	public IEnumerable<Archetype> ArchetypesAfterGeneration(ulong generation)
	{
		var idx = (int)generation;
		if (idx < 0 || idx >= _archetypes.Length) {
			EcsAssert.Panic(false, $"Generation {generation} is out of bounds.");
		}

		for (var i = idx + 1; i < _archetypes.Length; i++) {
			if (_archetypes[i] != null) {
				yield return _archetypes[i]!;
			}
		}
	}

	public IEnumerable<Archetype> Archetypes()
	{
		foreach (var archetype in _archetypes) {
			if (archetype != null) {
				yield return archetype;
			}
		}
	}
	public void Clear()
	{
		_archetypes = new Archetype?[1024];
		_byHashId.Clear();
	}
}