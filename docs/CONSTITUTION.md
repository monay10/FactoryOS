# FactoryOS Constitution

> **Ratified: 2026-07-19. This document is IMMUTABLE.**
> These rules govern every sprint, every commit, and every Claude Code session for the entire
> life of FactoryOS Enterprise. They cannot be changed, relaxed, or reinterpreted. If a task
> appears to require breaking a rule, the task stops and the conflict is surfaced — the rule is
> never worked around.

## The Rules

1. **Do not propose new features.** Build only what the active prompt specifies.
2. **Do not change the architecture** outside of an explicit prompt.
3. **Code is always production-grade.**
4. **Code is never left incomplete.**
5. **No `TODO`s.**
6. **No mock code.**
7. **No fake services.**
8. **No non-compiling code is ever produced.**
9. **The solution builds at the end of every sprint.**
10. **Unit tests run and pass.**
11. **Integration tests run and pass.**
12. **Code adheres to SOLID principles.**
13. **Clean Architecture is applied.**
14. **Domain-Driven Design is applied.**
15. **Event-Driven Architecture is used.**
16. **Plugin Architecture is used.**
17. **Multi-tenancy is supported.**
18. **All modules are independent.**
19. **The Core references no module.**
20. **Modules communicate only through interfaces.**
21. **Communication happens over the Event Bus.**
22. **All configuration is strongly typed.**
23. **XML documentation comments are written completely.**
24. **Code comments are written in English.**
25. **Documentation is produced in Markdown.**
26. **At the end of every sprint, changes are appended to `CHANGELOG.md`.**

---

*Operational architecture and build conventions that implement these rules live in
[../CLAUDE.md](../CLAUDE.md). Where any document conflicts with this Constitution, this
Constitution wins.*
