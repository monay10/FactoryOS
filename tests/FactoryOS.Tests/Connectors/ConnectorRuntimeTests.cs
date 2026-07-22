using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Connectors.Framework.Security;
using FactoryOS.Connectors.Runtime.Discovery;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Events;
using FactoryOS.Connectors.Runtime.Execution;
using FactoryOS.Connectors.Runtime.Integration;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Connectors.Runtime.Pipeline;
using FactoryOS.Connectors.Runtime.Security;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Tests.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Tests.Connectors;

public sealed class ConnectorRuntimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 22, 08, 00, 00, TimeSpan.Zero);

    private const string Tenant = "acme";
    private const string Other = "borusan";

    // ---------------------------------------------------------------------------------------------------
    // Permissions
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("connector.read", "connector.read", true)]
    [InlineData("connector.*", "connector.write", true)]
    [InlineData("*.*", "connector.execute", true)]
    [InlineData("connector.read", "connector.write", false)]
    [InlineData("workflow.read", "connector.read", false)]
    public void A_permission_grants_exactly_what_its_segments_cover(string held, string requested, bool grants)
    {
        Assert.Equal(grants, ConnectorPermission.Parse(held).Grants(ConnectorPermission.Parse(requested)));
    }

    [Theory]
    [InlineData("connector")]
    [InlineData("connector.read.extra")]
    [InlineData("")]
    [InlineData(null)]
    public void Something_that_is_not_a_permission_never_parses(string? value)
    {
        Assert.False(ConnectorPermission.TryParse(value, out _));
    }

    [Fact]
    public void Every_capability_maps_onto_a_permission_the_catalogue_recognises()
    {
        foreach (var capability in new[]
                 {
                     ConnectorCapability.Read, ConnectorCapability.Write, ConnectorCapability.Events,
                     ConnectorCapability.Commands, ConnectorCapability.Files, ConnectorCapability.Streaming,
                 })
        {
            Assert.Contains(ConnectorPermissions.For(capability), ConnectorPermissions.Catalogue);
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // Definitions, operations and categories
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_definition_derived_from_a_manifest_offers_the_operations_its_capabilities_imply()
    {
        var manifest = Manifest("logo");

        var read = ConnectorDefinition.FromManifest(
            manifest, new ConnectorVersion(1, 0, 0), ConnectorCapability.Read);
        var both = ConnectorDefinition.FromManifest(
            manifest, new ConnectorVersion(1, 0, 0), ConnectorCapability.Read | ConnectorCapability.Write);

        Assert.Equal(["read"], read.Operations.Select(operation => operation.Name));
        Assert.Equal(["read", "deliver"], both.Operations.Select(operation => operation.Name));
    }

    [Fact]
    public void An_operation_is_found_however_its_name_is_cased()
    {
        var definition = Definition("logo", ConnectorOperation.Read());

        Assert.NotNull(definition.FindOperation("READ"));
        Assert.Null(definition.FindOperation("write"));
    }

    [Fact]
    public void Resilience_is_narrowed_by_whichever_layer_knows_most()
    {
        var operationPolicy = ConnectorResiliencePolicy.Default with
        {
            Retry = new ConnectorRetryPolicy { MaxAttempts = 9 },
        };
        var instancePolicy = ConnectorResiliencePolicy.Default with
        {
            Retry = new ConnectorRetryPolicy { MaxAttempts = 5 },
        };

        var plain = ConnectorOperation.Read();
        var narrowed = plain with { Resilience = operationPolicy };
        var definition = Definition("logo", plain, narrowed with { Name = "narrow" });
        var instance = Instance(Tenant, "acme-logo", "logo");
        instance.UseResilience(instancePolicy);

        Assert.Equal(9, ConnectorDispatcher.ResilienceFor(definition, instance, narrowed).Retry.MaxAttempts);
        Assert.Equal(5, ConnectorDispatcher.ResilienceFor(definition, instance, plain).Retry.MaxAttempts);
    }

    [Fact]
    public void Every_category_belongs_to_exactly_one_family()
    {
        var categories = ConnectorCategories.All();

        Assert.Equal(33, categories.Count);
        Assert.All(categories, category => Assert.NotEqual(ConnectorType.Unknown, ConnectorCategories.TypeOf(category)));
        Assert.Equal(
            categories.Count,
            Enum.GetValues<ConnectorType>()
                .Where(type => type != ConnectorType.Unknown)
                .Sum(type => ConnectorCategories.InFamily(type).Count));
    }

    // ---------------------------------------------------------------------------------------------------
    // Retry
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_retry_delay_grows_geometrically_and_stops_at_its_ceiling()
    {
        var policy = new ConnectorRetryPolicy
        {
            MaxAttempts = 6,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2,
            MaxDelay = TimeSpan.FromMilliseconds(400),
        };

        Assert.Equal(TimeSpan.Zero, policy.DelayBefore(1));
        Assert.Equal(TimeSpan.FromMilliseconds(100), policy.DelayBefore(2));
        Assert.Equal(TimeSpan.FromMilliseconds(200), policy.DelayBefore(3));
        Assert.Equal(TimeSpan.FromMilliseconds(400), policy.DelayBefore(4));
        Assert.Equal(TimeSpan.FromMilliseconds(400), policy.DelayBefore(9));
    }

    [Fact]
    public void A_retry_policy_that_allows_no_attempt_at_all_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectorRetryPolicy { MaxAttempts = 0 });
    }

    [Fact]
    public void A_non_idempotent_operation_is_never_retried_however_transient_the_failure()
    {
        var engine = new RetryEngine(new RecordingDelay());
        var policy = new ConnectorRetryPolicy { MaxAttempts = 5 };
        var write = ConnectorOperation.Deliver();

        Assert.False(engine.ShouldRetry(policy, write, ConnectorError.Transient("x", "x"), 1));
        Assert.True(engine.ShouldRetry(policy, write with { Idempotent = true }, ConnectorError.Transient("x", "x"), 1));
    }

    [Theory]
    [InlineData(ConnectorErrorKind.Transient, true)]
    [InlineData(ConnectorErrorKind.Timeout, true)]
    [InlineData(ConnectorErrorKind.Throttled, true)]
    [InlineData(ConnectorErrorKind.Validation, false)]
    [InlineData(ConnectorErrorKind.Forbidden, false)]
    [InlineData(ConnectorErrorKind.CircuitOpen, false)]
    [InlineData(ConnectorErrorKind.Permanent, false)]
    public void Only_a_failure_a_later_attempt_could_survive_is_retryable(ConnectorErrorKind kind, bool retryable)
    {
        Assert.Equal(retryable, new ConnectorError("code", "message", kind).IsRetryable);
    }

    [Fact]
    public async Task A_transient_failure_is_retried_up_to_the_limit_and_then_reported()
    {
        var delay = new RecordingDelay();
        var harness = new Harness(delay: delay);
        var handler = new StubHandler("flaky", _ => ConnectorResponse.Failed(ConnectorError.Transient("io", "down")));

        harness.Activate(
            handler,
            Definition("flaky", ConnectorOperation.Read()) with
            {
                Resilience = ConnectorResiliencePolicy.Default with
                {
                    Retry = new ConnectorRetryPolicy { MaxAttempts = 3 },
                },
            });

        var response = await harness.InvokeAsync("read");

        Assert.False(response.Succeeded);
        Assert.Equal(3, handler.Calls);
        Assert.Equal(3, response.Attempts);
        Assert.Equal(2, delay.Delays.Count);
    }

    [Fact]
    public async Task A_retry_that_succeeds_reports_how_many_attempts_it_took()
    {
        var harness = new Harness(delay: new RecordingDelay());
        var attempt = 0;
        var handler = new StubHandler(
            "flaky",
            _ => ++attempt < 2 ? ConnectorResponse.Failed(ConnectorError.Transient("io", "down")) : ConnectorResponse.Ok("ok"));

        harness.Activate(
            handler,
            Definition("flaky", ConnectorOperation.Read()) with
            {
                Resilience = ConnectorResiliencePolicy.Default with
                {
                    Retry = new ConnectorRetryPolicy { MaxAttempts = 3 },
                },
            });

        var response = await harness.InvokeAsync("read");

        Assert.True(response.Succeeded);
        Assert.Equal(2, response.Attempts);
    }

    // ---------------------------------------------------------------------------------------------------
    // Circuit breaker
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_circuit_opens_only_after_the_threshold_is_reached()
    {
        var clock = new MutableClock(Now);
        var breaker = new CircuitBreakerEngine(clock);
        var policy = new ConnectorCircuitBreaker { FailureThreshold = 3 };

        for (var i = 0; i < 2; i++)
        {
            Assert.True(breaker.TryEnter("k", policy));
            breaker.RecordFailure("k", policy, ConnectorError.Transient("io", "down"));
        }

        Assert.Equal(CircuitState.Closed, breaker.Snapshot("k").State);

        Assert.True(breaker.TryEnter("k", policy));
        breaker.RecordFailure("k", policy, ConnectorError.Transient("io", "down"));

        Assert.Equal(CircuitState.Open, breaker.Snapshot("k").State);
        Assert.False(breaker.TryEnter("k", policy));
    }

    [Fact]
    public void An_open_circuit_half_opens_once_its_break_has_elapsed_and_closes_on_a_good_trial()
    {
        var clock = new MutableClock(Now);
        var breaker = new CircuitBreakerEngine(clock);
        var policy = new ConnectorCircuitBreaker { FailureThreshold = 1, BreakDuration = TimeSpan.FromSeconds(30) };

        breaker.TryEnter("k", policy);
        breaker.RecordFailure("k", policy, ConnectorError.Transient("io", "down"));
        Assert.False(breaker.TryEnter("k", policy));

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.True(breaker.TryEnter("k", policy));
        Assert.Equal(CircuitState.HalfOpen, breaker.Snapshot("k").State);

        breaker.RecordSuccess("k");
        Assert.Equal(CircuitState.Closed, breaker.Snapshot("k").State);
    }

    [Fact]
    public void A_failed_trial_call_re_opens_the_circuit_immediately()
    {
        var clock = new MutableClock(Now);
        var breaker = new CircuitBreakerEngine(clock);
        var policy = new ConnectorCircuitBreaker { FailureThreshold = 5, BreakDuration = TimeSpan.FromSeconds(30) };

        for (var i = 0; i < 5; i++)
        {
            breaker.TryEnter("k", policy);
            breaker.RecordFailure("k", policy, ConnectorError.Transient("io", "down"));
        }

        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.True(breaker.TryEnter("k", policy));
        breaker.RecordFailure("k", policy, ConnectorError.Transient("io", "down"));

        Assert.Equal(CircuitState.Open, breaker.Snapshot("k").State);
        Assert.False(breaker.TryEnter("k", policy));
    }

    [Theory]
    [InlineData(ConnectorErrorKind.Validation)]
    [InlineData(ConnectorErrorKind.Forbidden)]
    [InlineData(ConnectorErrorKind.Unauthorized)]
    [InlineData(ConnectorErrorKind.Throttled)]
    public void A_failure_that_is_the_callers_own_doing_never_opens_a_circuit(ConnectorErrorKind kind)
    {
        var breaker = new CircuitBreakerEngine(new MutableClock(Now));
        var policy = new ConnectorCircuitBreaker { FailureThreshold = 1 };

        breaker.TryEnter("k", policy);
        breaker.RecordFailure("k", policy, new ConnectorError("code", "message", kind));

        Assert.Equal(CircuitState.Closed, breaker.Snapshot("k").State);
    }

    [Fact]
    public async Task An_open_circuit_refuses_without_calling_the_external_system()
    {
        var harness = new Harness();
        var handler = new StubHandler("flaky", _ => ConnectorResponse.Failed(ConnectorError.Permanent("x", "down")));

        harness.Activate(
            handler,
            Definition("flaky", ConnectorOperation.Read()) with
            {
                Resilience = ConnectorResiliencePolicy.Default with
                {
                    Circuit = new ConnectorCircuitBreaker { FailureThreshold = 2 },
                },
            });

        await harness.InvokeAsync("read");
        await harness.InvokeAsync("read");
        var callsBefore = handler.Calls;

        var refused = await harness.InvokeAsync("read");

        Assert.Equal(callsBefore, handler.Calls);
        Assert.Equal(ConnectorErrorKind.CircuitOpen, refused.Error?.Kind);
    }

    // ---------------------------------------------------------------------------------------------------
    // Rate limit
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_rate_limit_refuses_once_its_permits_are_spent_and_frees_them_with_the_next_window()
    {
        var clock = new MutableClock(Now);
        var limiter = new RateLimiter(clock);
        var limit = new ConnectorRateLimit { Permits = 2, Window = TimeSpan.FromSeconds(1) };

        Assert.True(limiter.TryAcquire("k", limit));
        Assert.True(limiter.TryAcquire("k", limit));
        Assert.False(limiter.TryAcquire("k", limit));
        Assert.Equal(0, limiter.Snapshot("k", limit).Remaining);

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(limiter.TryAcquire("k", limit));
    }

    [Fact]
    public void One_factory_exhausting_its_quota_does_not_throttle_another()
    {
        var limiter = new RateLimiter(new MutableClock(Now));
        var limit = new ConnectorRateLimit { Permits = 1, Window = TimeSpan.FromSeconds(1) };

        Assert.True(limiter.TryAcquire(RateLimiter.KeyFor(Tenant, "logo"), limit));
        Assert.False(limiter.TryAcquire(RateLimiter.KeyFor(Tenant, "logo"), limit));
        Assert.True(limiter.TryAcquire(RateLimiter.KeyFor(Other, "logo"), limit));
    }

    [Fact]
    public async Task A_throttled_invocation_is_refused_without_reaching_the_connector()
    {
        var harness = new Harness();
        var handler = new StubHandler("logo", _ => ConnectorResponse.Ok("ok"));

        harness.Activate(
            handler,
            Definition("logo", ConnectorOperation.Read()) with
            {
                Resilience = ConnectorResiliencePolicy.Default with
                {
                    RateLimit = new ConnectorRateLimit { Permits = 1, Window = TimeSpan.FromMinutes(1) },
                },
            });

        Assert.True((await harness.InvokeAsync("read")).Succeeded);
        var refused = await harness.InvokeAsync("read");

        Assert.Equal(ConnectorErrorKind.Throttled, refused.Error?.Kind);
        Assert.Equal(1, handler.Calls);
    }

    // ---------------------------------------------------------------------------------------------------
    // Cache
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_cacheable_read_is_answered_from_the_cache_the_second_time()
    {
        var harness = new Harness();
        var handler = new StubHandler("logo", _ => ConnectorResponse.Ok("stock"));
        harness.Activate(handler, Definition("logo", Cacheable()) with { Resilience = CachingResilience() });

        var first = await harness.InvokeAsync("read");
        var second = await harness.InvokeAsync("read");

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("stock", second.PayloadAs<string>());
    }

    [Fact]
    public async Task An_operation_that_is_not_cacheable_is_asked_again_every_time()
    {
        var harness = new Harness();
        var handler = new StubHandler("logo", _ => ConnectorResponse.Ok("stock"));
        harness.Activate(handler, Definition("logo", ConnectorOperation.Read()) with { Resilience = CachingResilience() });

        await harness.InvokeAsync("read");
        await harness.InvokeAsync("read");

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task A_failure_is_never_cached()
    {
        var harness = new Harness();
        var handler = new StubHandler("logo", _ => ConnectorResponse.Failed(ConnectorError.Permanent("x", "down")));
        harness.Activate(handler, Definition("logo", Cacheable()) with { Resilience = CachingResilience() });

        await harness.InvokeAsync("read");
        await harness.InvokeAsync("read");

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public void A_cached_answer_expires_when_its_freshness_window_has_passed()
    {
        var clock = new MutableClock(Now);
        var cache = new ConnectorResponseCache(clock);
        var policy = new ConnectorCachePolicy { TimeToLive = TimeSpan.FromSeconds(60) };
        var operation = Cacheable();
        var request = ConnectorRequest.For(Tenant, "acme-logo", "read");

        cache.Store(request, ConnectorResponse.Ok("stock"), policy, operation);
        Assert.NotNull(cache.Find(request, policy, operation));

        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Null(cache.Find(request, policy, operation));
    }

    [Fact]
    public void Invalidating_one_tenants_cache_leaves_every_other_tenant_untouched()
    {
        var cache = new ConnectorResponseCache(new MutableClock(Now));
        var policy = new ConnectorCachePolicy();
        var operation = Cacheable();
        var mine = ConnectorRequest.For(Tenant, "logo", "read");
        var theirs = ConnectorRequest.For(Other, "logo", "read");

        cache.Store(mine, ConnectorResponse.Ok("a"), policy, operation);
        cache.Store(theirs, ConnectorResponse.Ok("b"), policy, operation);

        Assert.Equal(1, cache.InvalidateTenant(Tenant));
        Assert.Null(cache.Find(mine, policy, operation));
        Assert.NotNull(cache.Find(theirs, policy, operation));
    }

    // ---------------------------------------------------------------------------------------------------
    // Sessions
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_session_is_shared_across_invocations_and_lapses_once_it_stops_being_used()
    {
        var harness = new Harness();
        var sessions = harness.Provider.GetRequiredService<ConnectorSessionManager>();
        var instance = Instance(Tenant, "acme-logo", "logo");

        var first = sessions.Acquire(instance);
        var second = sessions.Acquire(instance);
        Assert.Equal(first.Id, second.Id);

        harness.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Null(sessions.Find(Tenant, "acme-logo"));
        Assert.Equal(1, sessions.Reap());
    }

    [Fact]
    public void A_session_belongs_to_one_tenants_instance_and_no_other()
    {
        var harness = new Harness();
        var sessions = harness.Provider.GetRequiredService<ConnectorSessionManager>();

        sessions.Acquire(Instance(Tenant, "logo", "logo"));

        Assert.NotNull(sessions.Find(Tenant, "logo"));
        Assert.Null(sessions.Find(Other, "logo"));
        Assert.Empty(sessions.ListByTenant(Other));
    }

    // ---------------------------------------------------------------------------------------------------
    // Secrets and credentials
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_secret_placeholder_is_resolved_from_the_secret_source()
    {
        var harness = new Harness();
        harness.Secrets.Set("ACME_LOGO", "s3cret");

        var resolved = harness.Resolver.Resolve("${secret:ACME_LOGO}");

        Assert.True(resolved.HasValue);
        Assert.Equal("s3cret", resolved.Reveal());
    }

    [Fact]
    public void A_placeholder_naming_a_secret_nobody_holds_resolves_to_nothing()
    {
        var harness = new Harness();

        Assert.False(harness.Resolver.Resolve("${secret:MISSING}").HasValue);
        Assert.True(harness.Resolver.IsReference("${secret:MISSING}"));
    }

    [Fact]
    public void An_encrypted_value_is_decrypted_by_the_frameworks_existing_protector()
    {
        var key = new byte[32];
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = (byte)i;
        }

        var protector = new AesGcmConnectorSecretProtector(key);
        var resolver = new ConnectorSecretResolver(
            [new InMemoryConnectorSecretSource()], protector, new InMemoryConnectorCredentialStore());

        var resolved = resolver.Resolve(protector.Protect("hunter2"));

        Assert.Equal("hunter2", resolved.Reveal());
    }

    [Fact]
    public void Formatting_a_secret_can_never_print_it()
    {
        var secret = new ConnectorSecret("hunter2");

        Assert.Equal("***", secret.ToString());
        Assert.DoesNotContain("hunter2", $"{secret}", StringComparison.Ordinal);
        Assert.Equal(string.Empty, ConnectorSecret.Empty.ToString());
    }

    [Fact]
    public void A_credential_with_no_resolvable_secret_is_not_usable()
    {
        var harness = new Harness();
        var credential = new ConnectorCredential
        {
            Key = "logo-sql",
            Kind = ConnectorCredentialKind.ConnectionString,
            SecretReference = "${secret:MISSING}",
        };

        Assert.False(harness.Resolver.Resolve(credential).IsComplete);
        Assert.True(ResolvedConnectorCredential.None.IsComplete);
    }

    [Fact]
    public void A_credential_may_be_kept_in_the_tenants_store_rather_than_repeated_on_every_instance()
    {
        var harness = new Harness();
        harness.Secrets.Set("SHARED", "value");
        harness.Provider.GetRequiredService<IConnectorCredentialStore>().Save(Tenant, new ConnectorCredential
        {
            Key = "shared",
            Kind = ConnectorCredentialKind.ApiKey,
            SecretReference = "${secret:SHARED}",
        });

        var instance = Instance(
            Tenant,
            "acme-logo",
            "logo",
            new ConnectorCredential { Key = "shared", Kind = ConnectorCredentialKind.ApiKey });

        Assert.Equal("value", harness.Resolver.ResolveFor(instance).Secret.Reveal());
    }

    // ---------------------------------------------------------------------------------------------------
    // Manifests and discovery
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_manifest_written_before_this_runtime_existed_still_reads()
    {
        const string json = """
            { "key": "logo", "name": "Logo", "sourceSystem": "Logo", "provides": [ "InventoryItem" ] }
            """;

        var result = ConnectorRuntimeManifestReader.Read(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ConnectorVersion(1, 0, 0), result.Value.Version);
        Assert.Equal(ConnectorCapability.Read, result.Value.Capabilities);
        Assert.Equal(ConnectorCategory.Unknown, result.Value.Category);
        Assert.Single(result.Value.Operations);
    }

    [Fact]
    public void A_manifest_may_declare_its_version_capabilities_category_and_operations()
    {
        const string json = """
            {
              "key": "logo", "name": "Logo", "sourceSystem": "Logo",
              "version": "2.1.3", "capabilities": "Read,Write", "category": "Logo",
              "operations": [
                { "name": "stock", "capability": "Read", "idempotent": true, "cacheable": true,
                  "requiredParameters": [ "warehouse" ], "timeoutSeconds": 45 }
              ]
            }
            """;

        var result = ConnectorRuntimeManifestReader.Read(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ConnectorVersion(2, 1, 3), result.Value.Version);
        Assert.Equal(ConnectorCategory.Logo, result.Value.Category);
        Assert.Equal(ConnectorType.Business, result.Value.Type);

        var operation = Assert.Single(result.Value.Operations);
        Assert.Equal("stock", operation.Name);
        Assert.Equal(ConnectorPermissions.Read, operation.Permission);
        Assert.Equal(["warehouse"], operation.RequiredParameters);
        Assert.Equal(TimeSpan.FromSeconds(45), operation.Timeout);
    }

    [Theory]
    [InlineData("\"version\": \"two\"", "Version")]
    [InlineData("\"capabilities\": \"Telepathy\"", "Capabilities")]
    [InlineData("\"category\": \"Telegraph\"", "Category")]
    public void A_manifest_the_runtime_cannot_make_sense_of_is_rejected_by_name(string field, string code)
    {
        var json = $$"""
            { "key": "logo", "name": "Logo", "sourceSystem": "Logo", {{field}} }
            """;

        var result = ConnectorRuntimeManifestReader.Read(json);

        Assert.True(result.IsFailure);
        Assert.Contains(code, result.Error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void An_operation_exercising_a_capability_the_connector_does_not_declare_is_rejected()
    {
        const string json = """
            {
              "key": "logo", "name": "Logo", "sourceSystem": "Logo", "capabilities": "Read",
              "operations": [ { "name": "push", "capability": "Write" } ]
            }
            """;

        var result = ConnectorRuntimeManifestReader.Read(json);

        Assert.True(result.IsFailure);
        Assert.Contains("does not declare", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovery_reports_a_broken_manifest_rather_than_quietly_skipping_it()
    {
        var root = Path.Combine(Path.GetTempPath(), "factoryos-connectors-" + Guid.NewGuid().ToString("N"));
        try
        {
            var good = Directory.CreateDirectory(Path.Combine(root, "logo"));
            File.WriteAllText(
                Path.Combine(good.FullName, "connector.json"),
                """{ "key": "logo", "name": "Logo", "sourceSystem": "Logo" }""");

            var bad = Directory.CreateDirectory(Path.Combine(root, "broken"));
            File.WriteAllText(Path.Combine(bad.FullName, "connector.json"), "{ \"name\": \"no key\" }");

            Directory.CreateDirectory(Path.Combine(root, "not-a-connector"));

            var results = new ConnectorDiscovery().Discover(root);

            Assert.Equal(2, results.Count);
            Assert.Single(results, result => result.Succeeded);
            Assert.Single(results, result => !result.Succeeded && result.Error is not null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Discovering_a_root_that_is_not_there_finds_nothing_rather_than_failing()
    {
        Assert.Empty(new ConnectorDiscovery().Discover(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    // ---------------------------------------------------------------------------------------------------
    // Compatibility and resolvers
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void An_operation_with_side_effects_may_not_be_cacheable()
    {
        var definition = Definition(
            "logo", ConnectorOperation.Read() with { Idempotent = false, Cacheable = true });

        var result = new CompatibilityValidator().Validate(definition);

        Assert.True(result.IsFailure);
        Assert.Contains("CacheableSideEffect", result.Error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_definition_declaring_the_same_operation_twice_is_refused()
    {
        var definition = Definition("logo", ConnectorOperation.Read(), ConnectorOperation.Read());

        Assert.True(new CompatibilityValidator().Validate(definition).IsFailure);
    }

    [Fact]
    public void A_definition_that_could_never_be_asked_anything_is_refused()
    {
        var validator = new CompatibilityValidator();

        Assert.True(validator.Validate(Definition("logo") with { Capabilities = ConnectorCapability.None }).IsFailure);
        Assert.True(validator.Validate(Definition("logo")).IsFailure);
    }

    [Theory]
    [InlineData(1, 2, 0, 1, 0, 0, true)]
    [InlineData(1, 0, 0, 1, 2, 0, false)]
    [InlineData(2, 0, 0, 1, 0, 0, false)]
    public void A_major_version_is_a_new_contract_and_never_satisfies_the_old_one(
        int major, int minor, int patch, int reqMajor, int reqMinor, int reqPatch, bool satisfies)
    {
        Assert.Equal(
            satisfies,
            new VersionResolver().Satisfies(
                new ConnectorVersion(major, minor, patch), new ConnectorVersion(reqMajor, reqMinor, reqPatch)));
    }

    [Fact]
    public void The_highest_version_of_a_connector_wins_when_several_are_present()
    {
        var resolver = new VersionResolver();
        var candidates = new[]
        {
            Definition("logo") with { Version = new ConnectorVersion(1, 0, 0) },
            Definition("logo") with { Version = new ConnectorVersion(1, 4, 2) },
            Definition("sap") with { Version = new ConnectorVersion(9, 0, 0) },
        };

        Assert.Equal(new ConnectorVersion(1, 4, 2), resolver.Highest(candidates, "logo")?.Version);
        Assert.Null(resolver.Highest(candidates, "netsis"));
    }

    [Fact]
    public void A_capability_finds_a_connector_without_the_caller_naming_one()
    {
        var resolver = new CapabilityResolver();
        var candidates = new[]
        {
            Definition("logo", ConnectorOperation.Read()),
            Definition("webhook", ConnectorOperation.Deliver()) with { Capabilities = ConnectorCapability.Write },
        };

        Assert.Equal(["logo"], resolver.Resolve(candidates, ConnectorCapability.Read).Select(d => d.Key));
        Assert.Equal(["webhook"], resolver.Resolve(candidates, ConnectorCapability.Write).Select(d => d.Key));
    }

    // ---------------------------------------------------------------------------------------------------
    // Loading and activation
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_handler_that_serves_another_connector_is_refused()
    {
        var harness = new Harness();

        var result = harness.Loader.Load(
            Definition("logo", ConnectorOperation.Read()), new StubHandler("sap", _ => ConnectorResponse.Ok()));

        Assert.True(result.IsFailure);
        Assert.Contains("KeyMismatch", result.Error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_declared_operation_the_handler_cannot_perform_stops_the_connector_loading()
    {
        var harness = new Harness();

        var result = harness.Loader.Load(
            Definition("logo", ConnectorOperation.Read(), ConnectorOperation.Deliver())
            with { Capabilities = ConnectorCapability.Read | ConnectorCapability.Write },
            new StubHandler("logo", _ => ConnectorResponse.Ok(), "read"));

        Assert.True(result.IsFailure);
        Assert.Contains("UnhandledOperation", result.Error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void Registering_and_loading_are_announced_separately()
    {
        var harness = new Harness();

        harness.Loader.Load(Definition("logo", ConnectorOperation.Read()), new StubHandler("logo", _ => ConnectorResponse.Ok()));

        Assert.Single(harness.Events.OfType<ConnectorRegistered>());
        Assert.Single(harness.Events.OfType<ConnectorLoaded>());
    }

    [Fact]
    public void A_tenant_cannot_activate_a_connector_that_is_not_registered()
    {
        var harness = new Harness();

        var result = harness.Engine.Activate(Instance(Tenant, "acme-logo", "logo"));

        Assert.True(result.IsFailure);
        Assert.Contains("NoDefinition", result.Error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void An_instance_faults_when_nothing_is_attached_that_can_perform_its_operations()
    {
        var harness = new Harness();
        harness.Loader.Register(Definition("logo", ConnectorOperation.Read()));
        harness.Engine.Activate(Instance(Tenant, "acme-logo", "logo"));

        var result = harness.Engine.Start(Tenant, "acme-logo");

        Assert.True(result.IsFailure);
        Assert.Equal(ConnectorStatus.Faulted, harness.Engine.Instances.Find(Tenant, "acme-logo")?.Status);
    }

    [Fact]
    public void An_instance_whose_secret_cannot_be_resolved_faults_before_a_shift_starts_on_it()
    {
        var harness = new Harness();
        harness.Loader.Load(Definition("logo", ConnectorOperation.Read()), new StubHandler("logo", _ => ConnectorResponse.Ok()));
        harness.Engine.Activate(Instance(
            Tenant,
            "acme-logo",
            "logo",
            new ConnectorCredential
            {
                Key = "logo-sql",
                Kind = ConnectorCredentialKind.ConnectionString,
                SecretReference = "${secret:MISSING}",
            }));

        var result = harness.Engine.Start(Tenant, "acme-logo");

        Assert.True(result.IsFailure);
        Assert.Contains("UnresolvedSecret", result.Error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void An_instance_pinned_to_a_version_it_does_not_have_refuses_to_start()
    {
        var harness = new Harness();
        harness.Loader.Load(
            Definition("logo", ConnectorOperation.Read()) with { Version = new ConnectorVersion(1, 0, 0) },
            new StubHandler("logo", _ => ConnectorResponse.Ok()));

        var instance = Instance(Tenant, "acme-logo", "logo");
        instance.RequireVersion(new ConnectorVersion(1, 5, 0));
        harness.Engine.Activate(instance);

        var result = harness.Engine.Start(Tenant, "acme-logo");

        Assert.True(result.IsFailure);
        Assert.Contains("VersionMismatch", result.Error.Code, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------------
    // Authorization and tenant isolation
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_caller_acting_in_another_tenant_can_never_reach_this_ones_connector()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var response = await harness.Engine.InvokeAsync(
            ConnectorRequest.For(Tenant, "acme-logo", "read") with
            {
                Caller = ConnectorCaller.Holding(Other, "u-eve", "connector.*"),
            });

        Assert.False(response.Succeeded);
        Assert.Contains("TenantMismatch", response.Error?.Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_authorizer_that_allows_everything_still_cannot_open_a_door_across_tenants()
    {
        // The authorizer is a port a host replaces. An adapter forwarding to a decision layer that only ever
        // sees the caller cannot know which tenant the instance belongs to, so the gate is not delegated to
        // it: the pipeline refuses before the port is consulted.
        var harness = new Harness(
            configure: services => services.AddSingleton<IConnectorAuthorizer>(new PermissiveAuthorizer()));
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var response = await harness.Engine.InvokeAsync(
            ConnectorRequest.For(Tenant, "acme-logo", "read") with
            {
                Caller = ConnectorCaller.Holding(Other, "u-eve", "connector.*"),
            });

        Assert.False(response.Succeeded);
        Assert.Contains("TenantMismatch", response.Error?.Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_naming_another_tenants_instance_finds_nothing_at_all()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var response = await harness.Engine.InvokeAsync(
            ConnectorRequest.For(Other, "acme-logo", "read") with
            {
                Caller = ConnectorCaller.Holding(Other, "u-eve", "connector.*"),
            });

        Assert.Equal(ConnectorErrorKind.NotFound, response.Error?.Kind);
    }

    [Fact]
    public async Task A_caller_holding_nothing_that_covers_the_operation_is_refused()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var response = await harness.InvokeAsync("read", ConnectorCaller.Holding(Tenant, "u-ada", "connector.write"));

        Assert.Equal(ConnectorErrorKind.Forbidden, response.Error?.Kind);
        Assert.Contains("MissingPermission", response.Error?.Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unauthenticated_caller_holds_nothing_whatever_it_claims()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var anonymous = ConnectorCaller.Anonymous(Tenant) with { Permissions = ["connector.*"] };
        var response = await harness.InvokeAsync("read", anonymous);

        Assert.Equal(ConnectorErrorKind.Unauthorized, response.Error?.Kind);
    }

    [Fact]
    public async Task A_request_that_names_nobody_is_refused()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var response = await harness.Engine.InvokeAsync(ConnectorRequest.For(Tenant, "acme-logo", "read"));

        Assert.Equal(ConnectorErrorKind.Unauthorized, response.Error?.Kind);
    }

    [Fact]
    public async Task A_disabled_instance_refuses_whatever_the_caller_holds()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));
        harness.Engine.Instances.Disable(Tenant, "acme-logo");

        var response = await harness.InvokeAsync("read");

        Assert.False(response.Succeeded);
        Assert.Contains("Disabled", response.Error?.Code, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------------
    // Pipeline and validation
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void The_pipeline_runs_its_stages_in_the_documented_order()
    {
        var harness = new Harness();

        Assert.Equal(
            [
                "tracing", "metrics", "monitoring", "audit", "authorization", "validation", "authentication",
                "caching", "retry", "ratelimit", "circuitbreaker", "transformation",
            ],
            harness.Provider.GetRequiredService<ConnectorPipeline>().Stages());
    }

    [Fact]
    public async Task A_missing_required_parameter_is_refused_before_the_connector_is_touched()
    {
        var harness = new Harness();
        var handler = new StubHandler("logo", _ => ConnectorResponse.Ok("stock"));
        harness.Activate(
            handler,
            Definition("logo", ConnectorOperation.Read() with { RequiredParameters = ["warehouse"] }));

        var response = await harness.InvokeAsync("read");

        Assert.Equal(ConnectorErrorKind.Validation, response.Error?.Kind);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task An_operation_no_connector_offers_is_refused_by_the_dispatcher()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var response = await harness.InvokeAsync("dance");

        Assert.Equal(ConnectorErrorKind.NotFound, response.Error?.Kind);
    }

    [Fact]
    public async Task An_instance_that_was_never_started_is_refused()
    {
        var harness = new Harness();
        harness.Loader.Load(Definition("logo", ConnectorOperation.Read()), new StubHandler("logo", _ => ConnectorResponse.Ok()));
        harness.Engine.Activate(Instance(Tenant, "acme-logo", "logo"));

        var response = await harness.InvokeAsync("read");

        Assert.False(response.Succeeded);
        Assert.Contains("NotRunning", response.Error?.Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_transform_shapes_a_successful_answer_and_leaves_a_failure_alone()
    {
        var transform = new UppercaseTransform();
        var harness = new Harness(configure: services => services.AddSingleton<IConnectorTransform>(transform));
        var succeed = true;
        harness.Activate(new StubHandler(
            "logo",
            _ => succeed ? ConnectorResponse.Ok("stock") : ConnectorResponse.Failed(ConnectorError.Permanent("x", "down"))));

        Assert.Equal("STOCK", (await harness.InvokeAsync("read")).PayloadAs<string>());

        succeed = false;
        var failure = await harness.InvokeAsync("read");
        Assert.False(failure.Succeeded);
        Assert.Equal(1, transform.Applications);
    }

    // ---------------------------------------------------------------------------------------------------
    // Telemetry, audit and health
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_invocation_carries_the_callers_correlation_all_the_way_back()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var response = await harness.Engine.InvokeAsync(
            ConnectorRequest.For(Tenant, "acme-logo", "read") with
            {
                Caller = ConnectorCaller.Holding(Tenant, "u-ada", "connector.*"),
                Correlation = ConnectorCorrelation.For("wo-4711"),
            });

        Assert.Equal("wo-4711", response.Correlation.CorrelationId);
        Assert.NotNull(response.Correlation.RequestId);
        Assert.Equal("wo-4711", harness.Audit.ForTenant(Tenant).Single().Correlation.CorrelationId);
    }

    [Fact]
    public async Task An_invocation_with_no_correlation_is_given_one()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var response = await harness.InvokeAsync("read");

        Assert.False(response.Correlation.IsEmpty);
    }

    [Fact]
    public async Task Refusals_are_audited_as_carefully_as_successes()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        await harness.InvokeAsync("read", ConnectorCaller.Holding(Tenant, "u-mallory", "workflow.read"));

        var entry = Assert.Single(harness.Audit.ForTenant(Tenant));
        Assert.False(entry.Succeeded);
        Assert.Equal("u-mallory", entry.Subject);
        Assert.Equal("forbidden", entry.Outcome);
    }

    [Fact]
    public async Task The_runtime_counts_what_it_did_and_why_it_refused()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        await harness.InvokeAsync("read");
        await harness.InvokeAsync("read", ConnectorCaller.Holding(Tenant, "u-mallory", "workflow.read"));

        var snapshot = harness.Engine.Snapshot();

        Assert.Equal(2, snapshot.Invocations);
        Assert.Equal(1, snapshot.Successes);
        Assert.Equal(1, snapshot.Refusals);
        Assert.Equal(0.5, snapshot.SuccessRate);
        Assert.Equal(1, harness.Metrics.Total(ConnectorMetricNames.Refusals));
    }

    [Fact]
    public async Task A_successful_call_beats_the_connectors_heart_and_a_caller_error_does_not()
    {
        var harness = new Harness();
        var health = harness.Provider.GetRequiredService<IConnectorHealthService>();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        await harness.InvokeAsync("read");
        Assert.Equal(ConnectorHealthStatus.Healthy, health.GetHealth("logo").Status);

        await harness.InvokeAsync("read", ConnectorCaller.Holding(Tenant, "u-mallory", "workflow.read"));
        Assert.Equal(ConnectorHealthStatus.Healthy, health.GetHealth("logo").Status);
    }

    [Fact]
    public void An_instance_nothing_has_called_yet_is_unproven_rather_than_healthy()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        var report = harness.Engine.Health(Tenant, "acme-logo");

        Assert.Equal(ConnectorHealthStatus.Healthy, report.For(ConnectorHealthAspect.Liveness)?.Status);
        Assert.Equal(ConnectorHealthStatus.Unknown, report.For(ConnectorHealthAspect.Dependency)?.Status);
        Assert.Equal(ConnectorHealthStatus.Unknown, report.Status);
    }

    [Fact]
    public void The_worst_aspect_decides_an_instances_health()
    {
        var harness = new Harness();
        harness.Secrets.Set("PRESENT", "value");
        harness.Activate(
            new StubHandler("logo", _ => ConnectorResponse.Ok("stock")),
            credential: new ConnectorCredential
            {
                Key = "logo-sql",
                Kind = ConnectorCredentialKind.ConnectionString,
                SecretReference = "${secret:PRESENT}",
            });

        var instance = harness.Engine.Instances.Find(Tenant, "acme-logo")!;
        instance.Reconfigure(
            instance.Endpoint,
            new ConnectorCredential
            {
                Key = "logo-sql",
                Kind = ConnectorCredentialKind.ConnectionString,
                SecretReference = "${secret:ROTATED_AWAY}",
            });

        var report = harness.Engine.Health(Tenant, "acme-logo");

        Assert.Equal(ConnectorHealthStatus.Unhealthy, report.Status);
        Assert.Equal(ConnectorHealthStatus.Healthy, report.For(ConnectorHealthAspect.Liveness)?.Status);
        Assert.Equal(ConnectorHealthStatus.Unhealthy, report.For(ConnectorHealthAspect.Credential)?.Status);
        Assert.Contains(report.Problems, problem => problem.Aspect == ConnectorHealthAspect.Credential);
    }

    [Fact]
    public async Task A_health_verdict_is_announced_once_when_it_changes_and_not_again_while_it_holds()
    {
        var harness = new Harness();
        harness.Activate(new StubHandler("logo", _ => ConnectorResponse.Ok("stock")));

        // An unproven instance reads Unknown, which is where it started; nothing has changed yet.
        Assert.Equal(ConnectorHealthStatus.Unknown, harness.Engine.Health(Tenant, "acme-logo").Status);
        Assert.Empty(harness.Events.OfType<ConnectorHealthChanged>());

        await harness.InvokeAsync("read");

        Assert.Equal(ConnectorHealthStatus.Healthy, harness.Engine.Health(Tenant, "acme-logo").Status);
        harness.Engine.Health(Tenant, "acme-logo");

        var announced = Assert.Single(harness.Events.OfType<ConnectorHealthChanged>());
        Assert.Equal(ConnectorHealthStatus.Unknown, announced.Previous);
        Assert.Equal(ConnectorHealthStatus.Healthy, announced.Current);
    }

    [Fact]
    public async Task A_schedule_runs_when_it_is_due_and_not_before()
    {
        var harness = new Harness();
        var handler = new StubHandler("logo", _ => ConnectorResponse.Ok("stock"));
        harness.Activate(handler);

        harness.Engine.Scheduler.Schedule(new ConnectorSchedule(
            "stock-poll",
            ConnectorRequest.For(Tenant, "acme-logo", "read") with
            {
                Caller = ConnectorCaller.Holding(Tenant, "svc-poller", "connector.read"),
            },
            TimeSpan.FromMinutes(10)));

        Assert.Single(await harness.Engine.RunDueAsync());
        Assert.Empty(await harness.Engine.RunDueAsync());

        harness.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Single(await harness.Engine.RunDueAsync());
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task A_recovery_is_announced_when_a_failing_operation_succeeds_again()
    {
        var harness = new Harness();
        var succeed = false;
        harness.Activate(new StubHandler(
            "logo",
            _ => succeed ? ConnectorResponse.Ok("stock") : ConnectorResponse.Failed(ConnectorError.Permanent("x", "down"))));

        await harness.InvokeAsync("read");
        Assert.Single(harness.Events.OfType<ConnectorFailed>());

        succeed = true;
        await harness.InvokeAsync("read");

        Assert.Single(harness.Events.OfType<ConnectorRecovered>());
    }

    // ---------------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------------

    private static ConnectorManifest Manifest(string key) => new()
    {
        Key = key,
        Name = key,
        SourceSystem = key,
        Provides = ["InventoryItem"],
    };

    private static ConnectorDefinition Definition(string key, params ConnectorOperation[] operations) => new()
    {
        Key = key,
        Name = key,
        Version = new ConnectorVersion(1, 0, 0),
        SourceSystem = key,
        Capabilities = ConnectorCapability.Read,
        Operations = operations,
    };

    private static ConnectorOperation Cacheable() =>
        ConnectorOperation.Read() with { Cacheable = true };

    private static ConnectorResiliencePolicy CachingResilience() =>
        ConnectorResiliencePolicy.Default with { Cache = new ConnectorCachePolicy() };

    private static ConnectorInstance Instance(
        string tenant, string key, string definitionKey, ConnectorCredential? credential = null) =>
        new(tenant, key, definitionKey, new ConnectorEndpoint("host:1433/db"), credential);

    private sealed class RecordingDelay : IConnectorDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class StubHandler : IConnectorOperationHandler
    {
        private readonly Func<ConnectorInvocation, ConnectorResponse> _respond;
        private readonly HashSet<string> _operations;

        public StubHandler(
            string key, Func<ConnectorInvocation, ConnectorResponse> respond, params string[] operations)
        {
            ConnectorKey = key;
            _respond = respond;
            _operations = operations.Length == 0
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "read", "deliver" }
                : new HashSet<string>(operations, StringComparer.OrdinalIgnoreCase);
        }

        public string ConnectorKey { get; }

        public int Calls { get; private set; }

        public bool CanHandle(string operation) => _operations.Contains(operation);

        public Task<ConnectorResponse> ExecuteAsync(
            ConnectorInvocation invocation, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_respond(invocation));
        }
    }

    private sealed class PermissiveAuthorizer : IConnectorAuthorizer
    {
        public ConnectorAuthorization Authorize(
            ConnectorCaller? caller, ConnectorInstance instance, ConnectorOperation operation) =>
            ConnectorAuthorization.Allow("This authorizer allows everything.");
    }

    private sealed class UppercaseTransform : IConnectorTransform
    {
        public int Applications { get; private set; }

        public string Name => "uppercase";

        public bool AppliesTo(ConnectorInvocation invocation) => true;

        public ConnectorResponse Apply(ConnectorInvocation invocation, ConnectorResponse response)
        {
            Applications++;
            return response.PayloadAs<string>() is { } text
                ? response with { Payload = text.ToUpperInvariant() }
                : response;
        }
    }

    private sealed class Harness
    {
        public Harness(IConnectorDelay? delay = null, Action<IServiceCollection>? configure = null)
        {
            Clock = new MutableClock(Now);
            Secrets = new InMemoryConnectorSecretSource();

            var services = new ServiceCollection();

            // The connector framework's configuration provider reads the host configuration; a container
            // without one cannot construct it. Every real host has one, so the harness supplies an empty one.
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddSingleton<IDateTimeProvider>(Clock);
            services.AddSingleton(delay ?? new RecordingDelay());
            services.AddSingleton<IConnectorSecretSource>(Secrets);
            configure?.Invoke(services);
            services.AddConnectorRuntime();

            Provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        }

        public MutableClock Clock { get; }

        public InMemoryConnectorSecretSource Secrets { get; }

        public ServiceProvider Provider { get; }

        public ConnectorEngine Engine => Provider.GetRequiredService<ConnectorEngine>();

        public ConnectorLoader Loader => Provider.GetRequiredService<ConnectorLoader>();

        public ConnectorSecretResolver Resolver => Provider.GetRequiredService<ConnectorSecretResolver>();

        public InMemoryConnectorRuntimeEventSink Events =>
            Provider.GetRequiredService<InMemoryConnectorRuntimeEventSink>();

        public InMemoryConnectorAuditSink Audit => Provider.GetRequiredService<InMemoryConnectorAuditSink>();

        public InMemoryConnectorMetricSink Metrics => Provider.GetRequiredService<InMemoryConnectorMetricSink>();

        public void Activate(
            IConnectorOperationHandler handler,
            ConnectorDefinition? definition = null,
            ConnectorCredential? credential = null)
        {
            var resolved = definition ?? Definition(handler.ConnectorKey, ConnectorOperation.Read());
            Assert.True(Loader.Load(resolved, handler).IsSuccess);
            Assert.True(Engine.Activate(Instance(Tenant, "acme-logo", resolved.Key, credential)).IsSuccess);
            Assert.True(Engine.Start(Tenant, "acme-logo").IsSuccess);
        }

        public Task<ConnectorResponse> InvokeAsync(string operation, ConnectorCaller? caller = null) =>
            Engine.InvokeAsync(ConnectorRequest.For(Tenant, "acme-logo", operation) with
            {
                Caller = caller ?? ConnectorCaller.Holding(Tenant, "u-ada", "connector.*"),
            });
    }
}
