# Adding Seed Data

Seed data exists to make local development/test scenarios useful. It is not a hidden production migration or security bootstrap.

## Rules

- Keep seed behavior environment-gated.
- Make it idempotent.
- Use obviously fictional data.
- Do not include real customer/contact information.
- Do not hard-code production credentials/passwords.
- Use ASP.NET Core Identity APIs for roles/users rather than inserting password hashes.
- Preserve service database ownership: CRM seeds CRM data; Identity seeds identity data.
- Do not seed Audit history as if it were genuine user activity unless the scenario explicitly labels it synthetic.

## Recommended order

For the proposed service catalog:
1. Identity roles.
2. Optional local development users with safe development-only credentials supplied via dev configuration.
3. CRM Clients.
4. Projects linked to seeded Clients.
5. Tasks linked to seeded Projects.

## Verification

A seed integration test should run the seeder twice and prove:
- no duplicates,
- required relationships valid,
- no production-environment execution path,
- all timestamps/identifiers valid.
