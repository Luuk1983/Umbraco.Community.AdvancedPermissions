# Machtigingsconcepten

Advanced Permissions bouwt voort op het machtigingsmodel van Umbraco en voegt fijnmazigere controle toe. Deze pagina legt de kernconcepten uit die elke editor en weergave deelt.

<a id="allow-deny"></a>
## Toestaan, Weigeren en overerven

Elke machtigingsinvoer is **Toestaan** of **Weigeren**. Er is geen aparte "overerven"-waarde: een machtiging die je **niet instelt** heeft simpelweg geen invoer, en het knooppunt erft wat van toepassing is van de dichtstbijzijnde bovenliggende (of de rij Standaardmachtigingen).

- **Toestaan** verleent de machtiging.
- **Weigeren** trekt de machtiging actief in — ook een machtiging die van een bovenliggende werd geërfd.
- **Niet ingesteld (overerven)** behoudt wat het knooppunt anders zou erven.

Anders dan bij de ingebouwde machtigingen van Umbraco staat elke machtiging los: je kunt er één op een knooppunt overschrijven en de rest laten overerven.

<a id="scope"></a>
## Bereik

Elke invoer heeft een **bereik** dat bepaalt hoe ver de regel reikt:

- **Alleen dit knooppunt** — geldt voor het knooppunt zelf, niet voor de onderliggende.
- **Dit knooppunt en onderliggende** — geldt voor het knooppunt en alles eronder.
- **Alleen onderliggende** — geldt voor de onderliggende, maar niet voor het knooppunt zelf.

Hiermee druk je patronen uit als "vergrendel de takwortel, maar laat alles eronder open" in één invoer.

<a id="default-row"></a>
## De rij Standaardmachtigingen

De **rij Standaardmachtigingen** bovenaan de editors is een virtuele wortel die een basislijn instelt voor alle content. Invoeren hier gelden overal, tenzij een specifiekere invoer ze overschrijft.

<a id="all-users"></a>
## De groep Alle gebruikers

De **groep Alle gebruikers** is een basislijn die elke backoffice-gebruiker bereikt, ongeacht de groepen waartoe hij behoort. Combineer het met een Weigeren om een knooppunt in één invoer voor iedereen te vergrendelen. Expliciete groepsinvoeren kunnen het nog steeds overschrijven.

<a id="priority-override"></a>
## Prioriteitsoverschrijving

Als een gebruiker tot meerdere groepen behoort, kunnen die het oneens zijn, en de normale volgorde landt niet altijd waar je wilt. **Prioriteitsoverschrijving** is de bewuste noodoplossing: markeer de invoer die moet winnen, en die wint — ongeacht de andere groepen van de gebruiker. Gebruik het spaarzaam.

<a id="resolution"></a>
## Hoe conflicten worden opgelost

Bij het bepalen of een gebruiker een machtiging op een knooppunt heeft, verzamelt het pakket elke toepasselijke invoer van elke groep waartoe de gebruiker behoort (inclusief de groep Alle gebruikers), respecteert het bereik van elke invoer en past deze volgorde toe, hoogste eerst:

1. **Expliciet Weigeren** overschrijft al het andere.
2. **Expliciet Toestaan** overschrijft elke geërfde machtiging.
3. **Geërfd Weigeren** overschrijft een geërfd Toestaan.
4. **Geërfd Toestaan**.

Als nergens in de boom een invoer van toepassing is, is de standaard **Weigeren** voor contentmachtigingen (**Toestaan** voor invoegopties). Een prioriteitsoverschrijving stapt buiten deze volgorde en wint.
