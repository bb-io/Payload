# Blackbird.io Payload CMS

Blackbird is the new automation backbone for the language technology industry. Blackbird provides enterprise-scale automation and orchestration with a simple no-code/low-code platform. Blackbird enables ambitious organizations to identify, vet and automate as many processes as possible. Not just localization workflows, but any business and IT process. This repository represents an application that is deployable on Blackbird and usable inside the workflow editor.

## Introduction

<!-- begin docs -->

[Payload CMS](https://payloadcms.com/) is a modern, open-source headless CMS built with TypeScript and Node.js. It gives developers full control over their content schema while exposing a flexible REST API for reading and updating content in any shape. This Blackbird app lets you integrate Payload CMS into your localization and content workflows — search items, download localizable content as HTML, send it to a translation tool, and push translations back directly.

## Before setting up

Before connecting your Payload CMS instance to Blackbird, make sure you have the following:

- A running Payload CMS instance accessible over the network (self-hosted or cloud-hosted)
- Admin access to the Payload admin panel
- API keys enabled on at least one collection (usually **Users**)

### How to enable and create an API key

Payload CMS API keys are tied to individual documents in a collection that has the `useAPIKey` option enabled.

1. Open your Payload admin panel and navigate to **Settings → Collections**
2. Make sure the collection you want to authenticate with (typically **Users**) has `useAPIKey: true` in its schema
3. Open the admin panel and navigate to the collection (e.g., **Users**)
4. Open an existing document or create a new one
5. Scroll down to the **API Key** section and click **Generate API Key**
6. Copy the generated key — it will only be shown once

> **Note:** Payload CMS has no default value for the auth collection slug. You must explicitly provide the slug of the collection whose API key you are using (e.g., `users`).

## Connecting

1. Navigate to apps and search for **Payload CMS**
2. Click _Add Connection_
3. Name your connection for future reference, e.g. `My Payload instance`
4. Fill in the following fields:
   - **Base URL**: The root URL of your Payload CMS instance, without a trailing slash (e.g. `http://localhost:3000` or `https://my-payload.vercel.app`)
   - **API key**: The raw API key generated in the Payload admin panel (without any prefix)
   - **Auth collection slug (e.g. users)**: The slug of the collection the API key belongs to. This is typically `users` unless you have a custom authentication collection (e.g. `integrations`)
5. Click _Connect_
6. Confirm that the connection has appeared and the status is _Connected_

## Actions

### Content

All actions in this group work with any Payload CMS collection. You always specify the **Content type** (the collection slug, e.g. `posts`, `articles`, `pages`) and the **Content ID** where applicable.

- **Search content**: Returns all items from a given collection. Pagination is handled automatically — all pages are fetched and combined into a single result. Optionally filter by **Locale**.

- **Get content**: Returns a single content item by its **Content type** and **Content ID**. Optionally specify a **Locale** to retrieve a specific language version.

- **Download content**: Fetches a content item and converts its localizable fields into a structured HTML file ready to hand off to a translation tool. Metadata in the HTML `<head>` encodes the content identity (UCID) so the file can be uploaded back without any additional inputs.
  - **Locale** _(optional)_: When provided, only that locale's field values are included in the HTML (e.g. `en` downloads the English version only). When omitted, all locales are fetched (`locale=all`) and every language version appears in the file.
  - **Fields to exclude** _(optional)_: Field names to omit from the HTML output.
  - **Include reference content** _(optional)_: When enabled, the localizable fields of any referenced documents (depth 1) are included as separate `<article>` blocks inside the HTML. These will also be updated during **Upload content**.

- **Update content**: Updates specific fields of a content item for a given locale via a PATCH request. Provide parallel lists of **Field keys** and **Field values** — the action zips them together and sends a single PATCH.

- **Upload content**: Takes a translated **HTML or XLF file** and updates the corresponding Payload CMS content item. The action reads all required information (content type, ID, fields) directly from the file — no extra inputs needed beyond the **Target locale** to write into. If the file includes referenced document blocks, those are updated in a second pass; any individual reference update failure is collected in the **Error messages** output rather than stopping the whole action.

## Events

### Content

- **On content triggered**: A webhook-based trigger that fires when Payload CMS sends a request to the Blackbird callback URL. After adding this event to a Bird, copy the displayed callback URL and register it as a webhook in your Payload CMS instance's hook configuration (e.g., an `afterChange` hook on one or more collections). The event payload delivers **Event**, **Content type**, **Content ID**, and **Timestamp**.

## Feedback

Do you want to use this app or do you have feedback on our implementation? Reach out to us using the [established channels](https://www.blackbird.io/) or create an issue.

<!-- end docs -->
