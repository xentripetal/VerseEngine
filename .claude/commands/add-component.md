---
description: Template for adding a new component
---

Guide the user through creating a new ECS component.

Ask the user:
1. What should the component be called?
2. What data does it need to store?
3. Should it be a struct (value type) or class (reference type)?
4. What storage type? (Table for frequent iteration, SparseSet for frequent add/remove)
5. Is it a tag component (no data)?

Then create the component following VerseEngine conventions:
- Proper naming (suffix with "Component" if ambiguous)
- Use struct by default unless class is needed
- Include XML documentation
- Add any necessary attributes for storage type
- Show example usage in a system

Provide the code and ask where to create the file.
