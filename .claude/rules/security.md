# Security rules

- Enforce authentication and policy authorization on the API; Angular guards are navigation aids, not security controls.
- Use least-privilege policies tied to CRM capabilities such as Accounts.Read, Accounts.Write, Lifecycle.Change, Reports.Read, and Admin.Manage.
- Resolve the actor through ICurrentUser; never accept actor IDs from ordinary client requests.
- Treat account/contact data as confidential business data. Never write secrets, access tokens, full request bodies, or unnecessary personal data to logs.
- Normalize and validate email, phone, URLs, free text, paging, sorting, and identifiers server-side.
- Use parameterized EF Core queries. Do not create dynamic SQL from user input.
- Require anti-forgery protections when cookie authentication is used. Configure CORS narrowly when token authentication is used.
- Keep credentials and connection strings in environment configuration, user secrets, or managed secret stores.
- Apply rate limiting to authentication, search, imports, exports, and other abuse-prone endpoints when implemented.
- Security-sensitive changes require integration tests for unauthorized, forbidden, and authorized paths.


## Repository evidence

Do not assume literal project names, paths, namespaces, DbContext names, connection names, or package versions. Resolve them from the solution, project files, AppHost, ServiceDefaults, and existing source before editing. Examples in the toolkit are role labels only.
