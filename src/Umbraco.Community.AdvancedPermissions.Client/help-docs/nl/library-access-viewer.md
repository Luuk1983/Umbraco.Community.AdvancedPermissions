# Library Access Viewer

Bekijk de effectieve Library-machtigingen van een gebruiker of gebruikersgroep op elk item — met volledige onderbouwing.

## Waarvoor het dient

Dit is de alleen-lezen tegenhanger van de Library Permissions Editor. Hij toont de volledig opgeloste machtiging voor elke gebruiker of gebruikersgroep op elk library-item, gecombineerd over alle groepen waartoe ze behoren, en legt uit hoe elk resultaat tot stand kwam.

## Hoe je het gebruikt

1. **Kies een gebruiker of een gebruikersgroep** in de werkbalk.
2. **Blader door de Library-boom.** Elke cel toont of een machtiging is toegestaan of geweigerd voor het gekozen onderwerp.
3. **Klik op een cel** om de onderbouwing te openen: welke groep bijdroeg, vanaf welk item, en of de regel expliciet was ingesteld of geërfd.

## Goed om te weten

- Een gearceerde **N/A**-cel betekent dat de machtiging niet van toepassing is op dat soort item — bijvoorbeeld "Aanmaken" op een los item, of item-specifieke acties (Publiceren, Depubliceren, Dupliceren, Terugdraaien) op een map.

## Library-startnodes

De viewer houdt ook rekening met de **Library-startnodes** van het onderwerp, omdat Umbraco elke Library-map en elk item daarbuiten blokkeert voordat er naar machtigingen wordt gekeken.

- De **startnodekaart** onder de kiezer zegt of Library-startnodes het onderwerp beperken, en toont de geldende Library-startnodes met wie elke startnode heeft ingesteld.
- Een map of item dat het onderwerp niet kan bereiken, toont **geblokkeerde** cellen (grijs, gearceerd, met een slotje) in plaats van toestaan of weigeren: **Alleen navigeren (startnodes)** voor een map boven een startnode, **Niet zichtbaar (startnodes)** voor alles daarbuiten. Machtigingen die nooit voor een map of item gelden, blijven **N/A**.
- Klik op een geblokkeerde cel om te zien waarom, en wat de machtigingen zouden zijn als het binnen een startnode viel.

Zie het tabblad **Concepten** voor hoe machtigingen worden opgelost.
