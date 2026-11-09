# User interface languages

Public pages support English (`en`, the default) and Vietnamese (`vi`). The header selector posts to `/language` with an antiforgery token and saves the choice in the `.TourismMvc.Language` cookie for one year. Browser language preferences do not override the English default. Admin pages keep their existing Vietnamese interface.

`UserText` uses Vietnamese source strings as keys. Add the English value in `user.en.json`, then render text with `@L["Vietnamese source"]`. For dynamic text, use placeholders such as `@L["Hành khách {0}", number]`. The catalog also translates matching server validation messages through `UserLocalizationFilter`.

The user layout exposes the same dictionary through `window.UserI18n.t(source, ...args)` for JavaScript messages. Dates and currency displays use `L.Culture` or `UserI18n.locale`. Keep submitted values, database values, identifiers and payment amounts unchanged when translating their visible labels. VNPAY receives `en` or `vn` according to the selected language.

Tour names, descriptions, itineraries and other editorial content stored in the database retain their original text. Translating that content requires separate language versions, for example a `tour_translations` table keyed by tour and language, with corresponding translations for itinerary days and policies. This UI feature does not change the database schema or automatically translate that content.

Local integration checks are in `artifacts/localization/LocalizationCheck.csproj`; browser checks are in `artifacts/localization/check-ui.cjs`. They use the isolated fixture managed by `artifacts/vnpay-check`, a preview server on port 5039 and a dedicated browser profile. Do not run fixture-dependent checks against real customer bookings.
