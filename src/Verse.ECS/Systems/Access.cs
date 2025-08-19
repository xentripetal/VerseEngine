using Verse.ECS.Scheduling;

namespace Verse.ECS.Systems;

/// <summary>
///     Tracks read and write access to specific elements in a collection.
///     Used internally to ensure soundness during system initialization and execution.
/// </summary>
/// <remarks>
///     Port of bevy_ecs::query::access::Access. Does not used a <see cref="FixedBitSet" /> as I'm trying to avoid
///     requiring a component registry
/// </remarks>
public struct Access : IEquatable<Access>
{
	/// <summary>
	///     Elements that are not accessed, but whose presence in an archetype affect query results
	/// </summary>
	public FixedBitSet Archetypal;
	/// <summary>
	///     Is true if this has access to all elements in the collection.
	///     This field is a performance optimization (also harder to mess up for soundness)
	/// </summary>
	public bool ReadsAll;
	/// <summary>
	///     All accessed elements
	/// </summary>
	public FixedBitSet ReadsAndWrites;
	/// <summary>
	///     Exclusively accessed elements
	/// </summary>
	public FixedBitSet Writes;
	/// <summary>
	///     Is true if this has mutable access to all elements in the collection.
	///     If this is true, then <see cref="ReadsAll" /> must also be true.
	/// </summary>
	public bool WritesAll;

	/// <summary>
	///     Adds access to the given type.
	/// </summary>
	/// <param name="type"></param>
	public Access AddRead(ulong id)
	{
		ReadsAndWrites[id] = true;
		return this;
	}

	/// <summary>
	///     Adds exclusive access to the given type.
	/// </summary>
	/// <param name="id"></param>
	public Access AddWrite(ulong id)
	{
		ReadsAndWrites[id] = true;
		Writes[id] = true;
		return this;
	}

	/// <summary>
	///     Adds an archetypal (indirect) access to the element given by `index`.
	///     This is for elements whose values are not accessed (and thus will never cause conflicts),
	///     but whose presence in an archetype may affect query results.
	/// </summary>
	/// <param name="id"></param>
	public Access AddArchetypal(ulong id)
	{
		Archetypal[id] = true;
		return this;
	}

	/// <summary>
	///     Returns `true` if this has read access to the given id.
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public bool HasRead(ulong id) => ReadsAll || ReadsAndWrites.Contains((int)id);

	/// <summary>
	///     Returns `true` if this can access anything
	/// </summary>
	/// <returns></returns>
	public bool HasAnyRead() => ReadsAll || ReadsAndWrites.HasAnySet();

	/// <summary>
	///     Returns `true` if this can exclusively access the given id.
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public bool HasWrite(ulong id) => WritesAll || Writes.Contains((int)id);

	/// <summary>
	///     Returns true if this can access anything exclusively
	/// </summary>
	/// <returns></returns>
	public bool HasAnyWrite() => WritesAll || Writes.HasAnySet();

	/// <summary>
	///     Returns true if this has an archetypal (indirect) access to the element given by `index`.
	///     This is an element whose value is not accessed (and thus will never cause conflicts),
	///     but whose presence in an archeid may affect query results.
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public bool HasArchetypal(ulong id) => Archetypal.Contains((int)id);

	/// <summary>
	///     Sets this as having access to all ids.
	/// </summary>
	public Access SetReadsAll()
	{
		ReadsAll = true;
		return this;
	}

	/// <summary>
	///     Sets this as having exclusive access to all ids.
	/// </summary>
	public Access SetWritesAll()
	{
		WritesAll = true;
		ReadsAll = true;
		return this;
	}

	/// <summary>
	///     Remove all writes
	/// </summary>
	/// <returns></returns>
	public Access ClearWrites()
	{
		WritesAll = false;
		Writes.Clear();
		return this;
	}

	/// <summary>
	///     Removes all accesses
	/// </summary>
	/// <returns></returns>
	public Access Clear()
	{
		ReadsAll = false;
		WritesAll = false;
		ReadsAndWrites.Clear();
		Writes.Clear();
		Archetypal.Clear();
		return this;
	}

	/// <summary>
	///     Adds all accesses from `other` to this.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	public Access Extend(Access other)
	{
		ReadsAndWrites.Or(other.ReadsAndWrites);
		Writes.Or(other.Writes);
		Archetypal.Or(other.Archetypal);
		ReadsAll = ReadsAll || other.ReadsAll;
		WritesAll = WritesAll || other.WritesAll;
		return this;
	}

	/// <summary>
	///     Returns `true` if the access and `other` can be active at the same time.
	///     <see cref="Access{T}" /> instances are incompatible if one can write
	///     an element that the other can read or write.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	public bool IsCompatible(Access other)
	{
		if (WritesAll) {
			return !other.HasAnyRead();
		}
		if (other.WritesAll) {
			return !HasAnyRead();
		}
		if (ReadsAll) {
			return !other.HasAnyWrite();
		}
		if (other.ReadsAll) {
			return !HasAnyWrite();
		}
		return !Writes.Overlaps(other.ReadsAndWrites) && !other.Writes.Overlaps(ReadsAndWrites);
	}

	/// <summary>
	///     Returns a set of elements that the access and `other` cannot access at the same time.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	public FixedBitSet GetConflicts(Access other)
	{
		var conflicts = new FixedBitSet();
		if (ReadsAll) {
			conflicts.Or(other.Writes);
		}
		if (other.ReadsAll) {
			conflicts.Or(Writes);
		}
		if (WritesAll) {
			conflicts.Or(other.ReadsAndWrites);
		}
		if (other.WritesAll) {
			conflicts.Or(ReadsAndWrites);
		}

		conflicts.Or(Writes.And(other.ReadsAndWrites));
		conflicts.Or(ReadsAndWrites.And(other.Writes));
		return conflicts;
	}

	public IEnumerable<ulong> GetReadsAndWrites() => ReadsAndWrites.OnesUL();

	public IEnumerable<ulong> GetWrites() => Writes.OnesUL();

	public IEnumerable<ulong> GetArchetypal() => Archetypal.OnesUL();

	/// <summary>
	///     Evaluates if other contains at least all the values in this
	/// </summary>
	/// <param name="other">Potential superset</param>
	/// <returns>True if this set is a subset of other</returns>
	public bool IsSubset(Access other)
	{
		if (WritesAll) {
			return other.WritesAll;
		}
		if (other.WritesAll) {
			return true;
		}
		if (ReadsAll) {
			return other.ReadsAll;
		}
		if (other.ReadsAll) {
			return Writes.IsSubsetOf(other.Writes);
		}
		return ReadsAndWrites.IsSubsetOf(other.ReadsAndWrites) && Writes.IsSubsetOf(other.Writes);
	}

	public override bool Equals(object? obj) => obj is Access other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(ReadsAndWrites, Writes, ReadsAll, WritesAll, Archetypal);
	public bool Equals(Access other) => Archetypal.Equals(other.Archetypal) && ReadsAll == other.ReadsAll && ReadsAndWrites.Equals(other.ReadsAndWrites) && Writes.Equals(other.Writes) && WritesAll == other.WritesAll;
}

/// <summary>
///     A set of filters that describe table level filters for a query.
/// </summary>
/// <remarks>Based on bevy_ecs::query::access::AccessFilters{T}</remarks>
public struct AccessFilters : IEquatable<AccessFilters>
{
	public FixedBitSet With;
	public FixedBitSet Without;

	public bool IsRuledOutBy(AccessFilters other) => With.Overlaps(other.Without) || Without.Overlaps(other.With);

	public AccessFilters Clone() => new () {
		With = With.Clone(),
		Without = Without.Clone()
	};

	public bool Equals(AccessFilters other) => With.Equals(other.With) && Without.Equals(other.Without);

	public override bool Equals(object? obj) => obj is AccessFilters other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(With, Without);
}

public struct FilteredAccess : IEquatable<FilteredAccess>
{
	public Access Access;
	public FixedBitSet Required;
	/// <summary>
	///     An array of filter sets to express `With` or `Without` clauses in disjunctive normal form, for example:
	///     `Or{(With{A}, With{B})}`.
	///     Filters like `(With{A}, Or{(With{B}, Without{C})}` are expanded into `Or{((With{A}, With{B}), (With{A},
	///     Without{C}))}`.
	/// </summary>
	public AccessFilters[] FilterSets = [new AccessFilters()];

	public FilteredAccess()
	{
		Access = default;
		Required = default;
	}

	public FilteredAccess AddRead(ulong id)
	{
		Access.AddRead(id);
		Required.Set(id);
		AndWith(id);
		return this;
	}

	public FilteredAccess AddWrite(ulong id)
	{
		Access.AddWrite(id);
		Required.Set(id);
		AndWith(id);
		return this;
	}

	public FilteredAccess AddRequired(ulong id)
	{
		Required.Set(id);
		return this;
	}

	/// <summary>
	///     Adds a `With` filter: corresponds to a conjunction (AND) operation.
	///     Suppose we begin with `Or{(With{A}, With{B})}`, which is represented by an array of two `AccessFilter` instances.
	///     Adding `AND With{C}` via this method transforms it into the equivalent of  `Or{((With{A}, With{C}), (With{B},
	///     With{C}))}`.
	/// </summary>
	/// <param name="id"></param>
	public FilteredAccess AndWith(ulong id)
	{
		for (int i = 0; i < FilterSets.Length; i++) {
			var filter = FilterSets[i];
			filter.With.Set(id);
			FilterSets[i] = filter;
		}
		return this;
	}

	/// <summary>
	///     Adds a `Without` filter: corresponds to a conjunction (AND) operation.
	///     Suppose we begin with `Or{(With{A}, With{B})}`, which is represented by an array of two `AccessFilter` instances.
	///     Adding `AND Without{C}` via this method transforms it into the equivalent of  `Or{((With{A}, Without{C}), (With{B},
	///     Without{C}))}`.
	/// </summary>
	public FilteredAccess AndWithout(ulong id)
	{
		for (int i = 0; i < FilterSets.Length; i++) {
			var filter = FilterSets[i];
			filter.Without.Set(id);
			FilterSets[i] = filter;
		}
		return this;
	}

	/// <summary>
	///     Appends an array of filters: corresponds to a disjunction (OR) operation.
	///     As the underlying array of filters represents a disjunction,
	///     where each element (`AccessFilters`) represents a conjunction,
	///     we can simply append to the array.
	/// </summary>
	/// <param name="other"></param>
	public FilteredAccess AppendOr(FilteredAccess other)
	{
		var f = FilterSets.ToList();
		f.AddRange(other.FilterSets);
		FilterSets = f.ToArray();
		return this;
	}

	/// <summary>
	///     Adds all of the accesses from other to this
	/// </summary>
	/// <param name="other"></param>
	public FilteredAccess ExtendAccess(Access other)
	{
		Access.Extend(other);
		return this;
	}

	/// <summary>
	///     Returns true if this and other can be active at the same time.
	/// </summary>
	public bool IsCompatible(FilteredAccess other)
	{
		if (Access.IsCompatible(other.Access)) {
			return true;
		}

		// If the access instances are incompatible, we want to check that whether filters can
		// guarantee that queries are disjoint.
		// Since the `FilterSets` array represents a Disjunctive Normal Form formula ("ORs of ANDs"),
		// we need to make sure that each filter set (ANDs) rule out every filter set from the `other` instance.
		//
		// For example, `Query<&mut C, Or<(With<A>, Without<B>)>>` is compatible `Query<&mut C, (With<B>, Without<A>)>`,
		// but `Query<&mut C, Or<(Without<A>, Without<B>)>>` isn't compatible with `Query<&mut C, Or<(With<A>, With<B>)>>`.
		return FilterSets.All(filter => other.FilterSets.All(otherFilter => filter.IsRuledOutBy(otherFilter)));
	}

	/// <param name="other">Comparison Access</param>
	/// <returns>A vector of elements that this and other cannot access at the same time</returns>
	public FixedBitSet GetConflicts(FilteredAccess other)
	{
		if (!IsCompatible(other)) {
			return Access.GetConflicts(other.Access);
		}
		return new();
	}

	/// <summary>
	///     Adds all access and filters from `other`.
	///     Corresponds to a conjunction operation (AND) for filters.
	///     Extending `Or{(With{A}, Without{B})}` with `Or{(With{C}, Without{D})}` will result in
	///     `Or{((With{A}, With{C}), (With{A}, Without{D}), (Without{B}, With{C}), (Without{B}, Without{D}))}`.
	/// </summary>
	/// <param name="other"></param>
	public FilteredAccess Extend(FilteredAccess other)
	{
		Access.Extend(other.Access);
		Required.Or(other.Required);

		// We can aFilteredAccess allocating a new array of bitsets if `other` contains just a single set of filters:
		// in this case we can short-circuit by performing an in-place union for each bitset.
		if (other.FilterSets.Length == 1) {
			for (int i = 0; i < FilterSets.Length ; i++) {
				var filter = FilterSets[i];
				filter.With.Or(other.FilterSets[0].With);
				filter.Without.Or(other.FilterSets[0].Without);
				FilterSets[i] = filter;
			}
			return this;
		}

		var newFilterSets = new List<AccessFilters>(FilterSets.Length + other.FilterSets.Length);
		foreach (var filter in FilterSets) {
			foreach (var otherFilter in other.FilterSets) {
				var newFilter = filter.Clone();
				newFilter.With.Or(otherFilter.With);
				newFilter.Without.Or(otherFilter.Without);
				newFilterSets.Add(newFilter);
			}
		}
		FilterSets = newFilterSets.ToArray();
		return this;
	}

	/// <summary>
	///     Sets the underlying unfiltered access as having access to all indexed elements.
	/// </summary>
	public FilteredAccess ReadAll()
	{
		Access.SetReadsAll();
		return this;
	}

	/// <summary>
	///     Sets the underlying unfiltered access as having mutable access to all indexed elements.
	/// </summary>
	public FilteredAccess WriteAll()
	{
		Access.SetWritesAll();
		return this;
	}

	/// <summary>
	///     Evaluates if other contains at least all the values in this
	/// </summary>
	/// <param name="other">Potential superset</param>
	/// <returns>True if this set is a subset of other</returns>
	public bool IsSubset(FilteredAccess other) => Required.IsSubsetOf(other.Required) && Access.IsSubset(other.Access);

	/// <returns>The elements that this access filters for</returns>
	public IEnumerable<ulong> WithFilters()
	{
		return FilterSets.SelectMany(x => x.With);
	}

	/// <returns>The elements that this access filters out</returns>
	public IEnumerable<ulong> WithoutFilters()
	{
		return FilterSets.SelectMany(x => x.Without);
	}

	public bool Equals(FilteredAccess other)
		=> Access.Equals(other.Access) && Required.Equals(other.Required) && FilterSets.SequenceEqual(other.FilterSets);

	public override bool Equals(object? obj) => obj is FilteredAccess other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(Access, Required, FilterSets);
}

/// <summary>
///     A collection of <see cref="FilteredAccess{T}" /> instances.
///     Used internally to statically check if systems have conflicting access.
///     It stores multiple sets of accesses.
///     <list id="bullet">
///         <item>A combined set, which is the access of all filters in this set combined.</item>
///         <item>The set of access of each individual filters in this set </item>
///     </list>
/// </summary>
public struct FilteredAccessSet
{
	public Access CombinedAccess;
	public List<FilteredAccess> FilteredAccesses;

	public FilteredAccessSet()
	{
		CombinedAccess = new Access();
		FilteredAccesses = new List<FilteredAccess>();
	}

	/// <summary>
	///     If the id is already read, this will upgrade it to a write for each subaccess that reads it
	/// </summary>
	/// <param name="id"></param>
	/// <returns>true if there was a read to turn into a write</returns>
	public bool UpgradeReadToWrite(ulong id)
	{
		if (!CombinedAccess.ReadsAll && CombinedAccess.HasRead(id)) {
			return false;
		}
		foreach (var filter in FilteredAccesses) {
			// If the access includes a read of the write, we want to add the write to the access.
			if (filter.Access.ReadsAll || filter.Required.Contains(id)) {
				filter.AddWrite(id);
			}
		}
		CombinedAccess.AddWrite(id);
		return true;
	}

	/// <summary>
	///     Access conflict resolution happen in two steps:
	///     <list id="number">
	///         <item>
	///             A "coarse" check, if there is no mutual unfiltered conflict between
	///             `self` and `other`, we already know that the two access sets are
	///             compatible.
	///         </item>
	///         <item>
	///             A "fine grained" check, it kicks in when the "coarse" check fails.
	///             the two access sets might still be compatible if some of the accesses
	///             are restricted with the [`With`](super::With) or [`Without`](super::Without) filters so that access is
	///             mutually exclusive. The fine grained phase iterates over all filters in
	///             the `self` set and compares it to all the filters in the `other` set,
	///             making sure they are all mutually compatible.
	///         </item>
	///     </list>
	/// </summary>
	/// <param name="other">comparison access set</param>
	/// <returns>True if this and other can be active at the same time</returns>
	public bool IsCompatible(FilteredAccessSet other)
	{
		if (CombinedAccess.IsCompatible(other.CombinedAccess)) {
			return true;
		}
		foreach (var filtered in FilteredAccesses) {
			foreach (var otherFiltered in other.FilteredAccesses) {
				if (!filtered.IsCompatible(otherFiltered)) {
					return false;
				}
			}
		}
		return true;
	}

	/// <summary>
	///     Returns a vector of elements that this set and `other` cannot access at the same time.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	public FixedBitSet GetConflicts(FilteredAccessSet other)
	{
		var conflicts = new FixedBitSet();
		if (!CombinedAccess.IsCompatible(other.CombinedAccess)) {
			foreach (var filtered in FilteredAccesses) {
				foreach (var otherFiltered in other.FilteredAccesses) {
					conflicts.Or(filtered.GetConflicts(otherFiltered));
				}
			}
		}
		return conflicts;
	}

	/// <summary>
	///     Returns a set of elements that this set and `other` cannot access at the same time.
	/// </summary>
	public FixedBitSet GetConflictsSingle(FilteredAccess filteredAccess)
	{
		var conflicts = new FixedBitSet();
		if (!CombinedAccess.IsCompatible(filteredAccess.Access)) {
			foreach (var filtered in FilteredAccesses) {
				conflicts.Or(filtered.GetConflicts(filteredAccess));
			}
		}
		return conflicts;
	}

	/// <summary>
	///     Adds the filtered access to the set.
	/// </summary>
	public FilteredAccessSet Add(FilteredAccess filteredAccess)
	{
		CombinedAccess.Extend(filteredAccess.Access);
		FilteredAccesses.Add(filteredAccess);
		return this;
	}

	/// <summary>
	///     Adds a read access without filters to the set
	/// </summary>
	public FilteredAccessSet AddUnfilteredRead(ulong element)
	{
		var filter = new FilteredAccess();
		filter.AddRead(element);
		return Add(filter);
	}

	/// <summary>
	///     Adds a write access without filters to the set
	/// </summary>
	/// <param name="element"></param>
	public FilteredAccessSet AddUnfilteredWrite(ulong element)
	{
		var filter = new FilteredAccess();
		filter.AddWrite(element);
		return Add(filter);
	}

	/// <summary>
	///     Adds all the access from the passed set to this
	/// </summary>
	public FilteredAccessSet Extend(FilteredAccessSet filteredAccessSet)
	{
		CombinedAccess.Extend(filteredAccessSet.CombinedAccess);
		FilteredAccesses.AddRange(filteredAccessSet.FilteredAccesses);
		return this;
	}

	/// <summary>
	///     Marks the set as reading all possible elements of id T
	/// </summary>
	public FilteredAccessSet ReadAll()
	{
		CombinedAccess.SetReadsAll();
		return this;
	}

	/// <summary>
	///     Marks the set as writing all of T
	/// </summary>
	public FilteredAccessSet WriteAll()
	{
		CombinedAccess.SetWritesAll();
		return this;
	}

	/// <summary>
	///     Removes all accesses stored in this set
	/// </summary>
	public FilteredAccessSet Clear()
	{
		CombinedAccess.Clear();
		FilteredAccesses.Clear();
		return this;
	}
}