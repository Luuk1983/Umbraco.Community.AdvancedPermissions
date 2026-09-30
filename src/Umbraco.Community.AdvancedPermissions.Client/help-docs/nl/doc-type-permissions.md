# Document Type Permissions Editor

Bepaal welke documenttypen een gebruikersgroep mag aanmaken, en waar in de contentboom.

## Waarvoor het dient

In Umbraco stel je de toegestane onderliggende typen van een documenttype in, maar die keuze geldt voor iedereen. Deze editor gaat verder: hij bepaalt *per gebruikersgroep* of een documenttype onder een knooppunt mag worden aangemaakt. Zo kan de ene groep "Nieuwsartikel"-pagina's onder een sectie toevoegen terwijl de andere groep dat niet kan — zonder het documenttype zelf te wijzigen.

Aanmaken is standaard toegestaan (overal waar Umbraco's eigen toegestane onderliggende typen dat toelaten). Met deze editor voeg je **Toestaan**- of **Weigeren**-regels toe die dat voor een specifieke groep beperken of verruimen.

## Hoe je het gebruikt

1. **Kies een gebruikersgroep** en **kies een documenttype** in de werkbalk. Beide zijn nodig voordat de tabel laadt.
2. **Blader door de contentboom.** De rij Standaardmachtigingen bovenaan stelt een basislijn in voor de hele site; de rijen eronder zijn je contentknooppunten.
3. **Klik op een cel** in de kolom Invoegen om het bereikdialoogvenster te openen.
4. **Kies Toestaan, Weigeren of Niet ingesteld** voor dit knooppunt — en, als onderliggende moeten afwijken, een aparte status voor hen. Markeer eventueel Prioriteitsoverschrijving.
5. **Sla** je wijzigingen op.

## Goed om te weten

- Een gedimde cel of een **N/A**-cel betekent dat het documenttype op dat knooppunt sowieso geen toegestaan onderliggend type is. De regel wordt nog steeds opgelost, maar Umbraco zou het type daar niet aanbieden.
- Omdat aanmaken standaard is toegestaan, voeg je meestal **Weigeren** toe om specifieke typen te vergrendelen, of **Toestaan** om een type weer in te schakelen dat een bredere Weigeren had verwijderd.

Zie het tabblad **Concepten** voor Toestaan/Weigeren, bereik, de rij Standaardmachtigingen, de groep Alle gebruikers en Prioriteitsoverschrijving.

## Realtime updates

Terwijl je bewerkt, worden wijzigingen die elders zijn opgeslagen live naar dit scherm doorgevoerd.

- Een cel krijgt een **omlijning** wanneer deze door iemand anders is gewijzigd sinds je hem laadde. Een banner vermeldt hoeveel cellen conflicteerden.
- **Opgeslagen waarden laden** vervangt alleen de omlijnde cellen door wat nu is opgeslagen — alles wat je verder hebt gewijzigd blijft ongemoeid.
- **Mijn wijzigingen behouden** sluit de banner en behoudt je bewerkingen op het scherm, maar dat betekent niet dat je wijzigingen gewonnen hebben: de opgeslagen waarden zijn ongewijzigd, en opslaan zal je nog steeds om bevestiging vragen voordat ze worden overschreven.
