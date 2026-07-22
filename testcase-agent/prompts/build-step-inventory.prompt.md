Build a C# console tool in tools/build-step-inventory/ that generates a step inventory of our Selenium test solution for AI-assisted test-code generation.

Approach: use Roslyn (Microsoft.CodeAnalysis + MSBuildWorkspace) to load the solution and analyze it semantically — not regex. The solution has three kinds of projects: general/utility projects, page-object/element projects, and test-case projects (which also contain shared-step classes and test base classes).

What to inventory — three kinds of entries:

Page-object methods (kind: "page-object-method"): every public method on page-object classes. Capture the declared return type; when it is another page object, that is the page the caller lands on.
Shared steps (kind: "shared-step"): public methods in shared-step classes within the test-case projects. Additionally extract composes: the ordered list of page-object methods they call directly (one level deep is sufficient — resolve fluent chains like new Navigation(m).GoToOpportunityPage().OpenNewOpportunityForm() via the semantic model, listing each resolved call).
Base-class steps (kind: "base-step"): protected/public methods on test base classes. Capture availability: which test classes/namespaces inherit them — these are only callable from within that inheritance tree.

Fields per entry: id (stable, e.g. po.OpportunityPage.SelectQuery), kind, project, class, signature, returns (resolved type; flag when it is a page object), purpose (see below), composes (shared steps only), availability ("global" or the scope), usages (count of call sites across the solution, via FindReferences or call-site analysis).

purpose — fill only from evidence, never invent: use the XML doc comment if present; otherwise, if the method name is unambiguous, derive a short line from name + parameters; otherwise set purpose: null and add the id to a needs-description list in the output. Do not guess what unclear methods do.

Output: step-inventory.json (the machine artifact) and a step-inventory.md rendered from the same data, grouped by kind then by class, one line per entry. Also print run statistics: entries per kind, how many purposes are null, any projects or files that failed to load.

Practicalities: heuristics for classifying classes (page object vs. shared step vs. base class) should be configurable at the top of the program — start with reasonable guesses from project names and base types, print what classification each class got, and I will correct the heuristics. If MSBuildWorkspace fails to load the solution (SDK/version issues), report the exact error and stop — do not fall back to text parsing silently.

After the first successful run, show me: the statistics, five sample entries of each kind, and the needs-description count.