# Library Element Type Permissions

Bepaal welke elementtypen een gebruikersgroep in de Library mag aanmaken.

## Waarvoor het dient

Elementtypen zijn de bouwstenen die je in de Library kunt toevoegen. Standaard is elk elementtype aanmaakbaar. Met deze editor kun je, per gebruikersgroep, een elementtype uit de Library verbergen (**Weigeren**) of er expliciet één behouden (**Toestaan**).

Het is een **filter, geen toekenning** en werkt daarmee anders dan de boomgebaseerde rechten: het beperkt alleen de elementtypen die Umbraco al in de Library aanbiedt. Een Weigeren verbergt een type; een Toestaan laat nooit een type verschijnen dat geen geldig Library-elementtype is — het houdt een type aanmaakbaar en laat die keuze, met Prioriteitsoverschrijving, winnen van een Weigeren uit een andere groep van de gebruiker.

Anders dan de boomgebaseerde editors is deze keuze **sectiebreed** — ze is niet gekoppeld aan een specifiek knooppunt, omdat Umbraco geen bovenliggende meegeeft wanneer je een item in de Library aanmaakt.

## Hoe je het gebruikt

1. **Kies een gebruikersgroep** in de werkbalk.
2. Je ziet een lijst met alle elementtypen met één kolom voor aanmaken in de Library.
3. **Klik op de cel van een rij** om het dialoogvenster te openen.
4. **Kies Toestaan, Weigeren of Niet ingesteld.** "Niet ingesteld" laat het type standaard aanmaakbaar. Markeer eventueel Prioriteitsoverschrijving zodat deze keuze wint wanneer de gebruiker in meerdere groepen zit.
5. **Sla** je wijzigingen op.

## Goed om te weten

- Is de lijst leeg, dan zijn er nog geen documenttypen als elementtype gemarkeerd. Markeer een documenttype als elementtype en schakel "Toestaan in Library" in om het hier te laten verschijnen.

Zie het tabblad **Concepten** voor Toestaan/Weigeren en Prioriteitsoverschrijving.
