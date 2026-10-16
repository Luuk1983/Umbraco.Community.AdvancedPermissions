# Access Viewer

Beantwoord de vraag "wat kan deze gebruiker of groep hier echt doen?" — met een volledige uitleg.

## Waarvoor het dient

De Access Viewer toont de **effectieve** (volledig opgeloste) machtiging voor elke gebruiker of gebruikersgroep op elk contentknooppunt, en legt precies uit hoe elk resultaat tot stand kwam.

## Hoe je het gebruikt

1. **Kies een gebruiker of een gebruikersgroep** in de werkbalk.
2. **Blader door de contentboom.** Elke cel toont of een machtiging is toegestaan of geweigerd voor het gekozen onderwerp.
3. **Klik op een cel** om de redeneerketen te openen: welke groep bijdroeg, vanaf welk knooppunt, en of het expliciet of geërfd was.
4. Wanneer een **Prioriteitsoverschrijving** het resultaat veranderde, toont de redenering wat het zonder die overschrijving zou zijn geweest.

## Startnodes

De viewer houdt ook rekening met de **startnodes** van het onderwerp, omdat Umbraco elke node daarbuiten blokkeert voordat er naar machtigingen wordt gekeken.

- De **startnodekaart** onder de kiezer zegt of startnodes het onderwerp beperken, en toont de geldende startnodes met wie elke startnode heeft ingesteld: een gebruikersgroep, of het eigen account van de gebruiker. Ingestelde startnodes die niets veranderen (gedekt door een hogere, vervangen of verwijderd) worden niet getoond.
- Een node die het onderwerp niet kan bereiken, toont **geblokkeerde** cellen (grijs, gearceerd, met een slotje) in plaats van toestaan of weigeren:
  - **Alleen navigeren (startnodes)**: een bovenliggende node van een startnode. Umbraco toont hem in de boom zodat de gebruiker naar de startnode kan navigeren, maar hij kan niet worden geopend.
  - **Niet zichtbaar (startnodes)**: buiten alle startnodes, dus verborgen.
- Klik op een geblokkeerde cel om te zien waarom, en wat de machtigingen zouden zijn als de node binnen een startnode viel.
- Met een gebruikersgroep geselecteerd gebruikt de viewer de eigen startnode van die groep. Een gebruiker krijgt de gecombineerde startnodes van al hun groepen; kies dus een gebruiker om het echte resultaat te zien.

Zie het tabblad **Concepten** voor hoe machtigingen worden opgelost.
