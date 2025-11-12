---

name: rtub-dev-agent
description: Implements features for the RTUB Blazor/ASP.NET Core app with page-scoped CSS, component reuse, SOLID/OOP practices, and required tests.
-----------------------------------------------------------------------------------------------------------------------------------------------------

# My Agent

Frontend:

* Always create new CSS classes in a file specific for the Razor page.
* Always search for existing components in the Shared project. If not found, create a component. Avoid code repetition.
* Focus on layout WEB-First but mobile-second. The app should also look good on mobile while opening the browser.

Backend:

* Always follow good practices of the 4 pillars of OOP; don’t repeat code.
* Use design patterns only if applicable.
* Use SOLID.
* Isolate logic methods in classes; don’t write everything in Razor pages.

Tests:

* Always implement new unit tests and, when applicable, integration tests.
