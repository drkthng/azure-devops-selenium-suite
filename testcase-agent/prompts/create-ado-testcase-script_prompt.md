Build a C# console application in a new tools/ado-importer/ folder that imports approved test cases from our out/ files into Azure DevOps as Test Case work items.
Read these first, they define the mapping: style/testcase-schema.md (the exact structure of our test-case files — field names, and how steps are represented as action + optional-expected pairs) and one example file in out/. Derive the field mapping from these, do not assume a structure. The mapping is: title → System.Title; our steps → the Microsoft.VSTS.TCM.Steps XML field, where each step is a <step> with an action parameterizedString and an expectedResult parameterizedString only when our file specifies an expected result for that step — steps without one get an empty expectedResult; Area Path → System.AreaPath; our Source/Coverage info → a tag or the System.Description field, whichever fits the schema better.
Configuration, not hardcoding: organization URL, project name, target Area Path, and the PAT must come from environment variables or a config file (e.g. appsettings.json that is git-ignored) — never hardcoded, never committed. The PAT is a secret. Read it from an environment variable.
Authentication: authenticate to the Azure DevOps REST API using the PAT via basic auth (PAT as password, empty username, base64-encoded), or the Microsoft.TeamFoundationServer.Client SDK if you prefer — your call, but explain which you chose.
Safety behaviour, this is critical:

Take the input file path as a command-line argument. Process only that one file.
Support a --dry-run flag that parses the file and prints exactly what work items would be created (title, area path, step count) without calling the API. This must be the default if no write flag is given — never write unless explicitly told to.
Before creating anything, print a summary and the target (org, project, area path) so the user can confirm they're pointing at the right place.
On API errors, stop and report clearly — do not retry blindly in a way that could create duplicates.
Echo the created work item IDs and URLs at the end so they can be checked in ADO.

Do not add update, delete, or bulk-overwrite features. This tool only creates. Keep it minimal.
After building it, show me how to run it: first a --dry-run on one out/ file, then the real create for a single test case so I can verify the field mapping in ADO before importing in volume.