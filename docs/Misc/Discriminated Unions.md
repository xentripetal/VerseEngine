CSharp is in progress for defining a Union type. This is leaning towards not supporting multi-valued implementations, instead its close to a OneOf<T1, T2, ...>. 

I don't really like this semantic as it underlies a union is a pointer, not a value type.

So for now, going with a poor mans version of a union where each type just has its needed fields and has methods for treating it as union-ish