# Content Permissions Editor

Beheer alle machtigingsinvoeren van een gebruikersgroep over de contentboom op één plek.

## Waarvoor het dient

Bepaal precies wat elke gebruikersgroep op elk contentknooppunt mag — en hoe ver elke regel reikt — zonder de alles-of-niets-beperking van de ingebouwde machtigingen van Umbraco.

## Hoe je het gebruikt

1. **Kies een gebruikersgroep** in de werkbalk.
2. **Blader door de contentboom.** De rij Standaardmachtigingen bovenaan stelt een basislijn in voor alle content; de boom toont welke knooppunten invoeren hebben.
3. **Klik op een cel** (een knooppunt × machtiging) om het bereikdialoogvenster te openen.
4. **Kies de status** voor dit knooppunt en, indien afwijkend, een aparte status voor onderliggende. Markeer eventueel Prioriteitsoverschrijving.
5. **Sla** je wijzigingen op.

Zie [Machtigingsconcepten](concepts.md) voor Toestaan/Weigeren, bereik, de rij Standaardmachtigingen, de groep Alle gebruikers en Prioriteitsoverschrijving.

## Voorbeelden

**Een knooppunt vergrendelen tegen verwijderen.** Voeg één invoer toe: groep Alle gebruikers, Weigeren, Verwijderen, bereik "Alleen dit knooppunt". Het Weigeren wint voor iedereen.

**Een uitzondering maken op een breed Toestaan.** Editors hebben Toestaan voor Publiceren op Home met bereik "Dit knooppunt en onderliggende". Om publiceren onder de tak Persberichten te stoppen terwijl elke andere machtiging behouden blijft, voeg je één Weigeren voor Publiceren op Persberichten toe.
