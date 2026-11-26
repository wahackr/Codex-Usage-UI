using Xunit;

// FAKE_MODE is process-wide state shared with the FakeAppServer child
// process, so tests that spawn it must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
