# Sprawozdanie Projektowe: System Password Breaker
**Temat:** Rozproszony system odzyskiwania haseł w architekturze Master-Worker.

## 1. Wstęp
Projekt **Password Breaker** to wysokowydajna aplikacja rozproszona służąca do odzyskiwania haseł (hashy) metodą brute-force. System został zaprojektowany w celu zademonstrowania efektywnego podziału zadań obliczeniowych pomiędzy wiele jednostek wykonawczych (Workerów) oraz wizualizacji procesu w czasie rzeczywistym przy użyciu nowoczesnych technologii webowych.

## 2. Cel Projektu
Głównym celem było stworzenie skalowalnego środowiska, które:
*   **Minimalizuje czas obliczeń** poprzez równoległe przetwarzanie na wielu rdzeniach i wielu maszynach.
*   **Dynamicznie zarządza zasobami** (możliwość dodawania/usuwania workerów w trakcie pracy z poziomu GUI).
*   **Zapewnia precyzyjną analitykę** (wydajność H/s, dokładny czas obliczeń do setnych sekundy).
*   **Archiwizuje wyniki** w trwałej bazie danych PostgreSQL dla późniejszej analizy.

## 3. Architektura Systemu
System opiera się na modelu **Master-Worker (Orchestration)** i składa się z czterech głównych modułów:

### A. PasswordBreaker.Server (Master)
Serce systemu napisane w **.NET 10**. Pełni rolę koordynatora.
*   **Zarządzanie Kolejką (Chunking):** Dzieli przestrzeń poszukiwań na paczki (WorkChunks) po 1 000 000 haseł każda. Zapobiega to powielaniu pracy i umożliwia naturalny Load Balancing.
*   **Komunikacja SignalR:** Wykorzystuje protokół WebSocket do przesyłania zadań do workerów i odbierania statystyk "na żywo".
*   **Orkiestracja Procesów:** Odpowiada za fizyczne uruchamianie i zamykanie instancji workerów na żądanie z Frontendu (wykorzystuje `System.Diagnostics.Process`).

### B. PasswordBreaker.Worker (Węzeł Obliczeniowy)
Usługa typu Background Service zoptymalizowana pod kątem wydajności.
*   **TPL (Task Parallel Library):** Wykorzystuje `Parallel.For` do pełnego obciążenia przydzielonych rdzeni procesora.
*   **Granularność:** Każda instancja workera jest konfigurowalna (limit wątków), co pozwala na precyzyjne skalowanie mocy obliczeniowej (1 worker = 1 wątek).
*   **Mechanizm Pull:** Workery same proszą o pracę, co gwarantuje, że szybsze jednostki otrzymają więcej zadań.

### C. PasswordBreaker.Frontend (Dashboard)
Aplikacja typu SPA zbudowana w **React 19** i **TypeScript**.
*   **Real-time UI:** Dzięki SignalR wykresy i statystyki aktualizują się bez przeładowania strony.
*   **Zarządzanie Stanem:** Intuicyjny interfejs umożliwia konfigurację ataku (alfabet, długość, algorytm) oraz sterowanie flotą workerów.
*   **Nawigacja i Animacje:** Wykorzystano **Framer Motion** dla płynnych przejść między Dashboardem a Historią oraz efektownych animacji tabel.

### D. PasswordBreaker.Shared
Biblioteka wspólna zawierająca kontrakty danych, modele (AttackStatus, WorkChunk) oraz implementacje algorytmów (MD5, SHA256, Argon2).

## 4. Decyzje Projektowe i Technologie

| Technologia | Decyzja i Uzasadnienie |
| :--- | :--- |
| **.NET 10** | Wybrany ze względu na najwyższą wydajność w operacjach na ciągach znaków (`Span<T>`) oraz świetną obsługę wielowątkowości. |
| **PostgreSQL** | Relacyjna baza danych zapewniająca integralność danych historycznych i łatwą konteneryzację. |
| **Entity Framework Core** | Wykorzystany jako ORM do obsługi bazy danych, zapewniający łatwe migracje i asynchroniczny zapis wyników. |
| **SignalR** | Zapewnia dwukierunkową komunikację o niskich opóźnieniach, niezbędną do raportowania tysięcy haszy/s. |
| **Docker Compose** | Konteneryzacja całego stosu umożliwia uruchomienie systemu jedną komendą (`docker-compose up`). |
| **Framer Motion** | Zastosowany do animacji zakładek i tabeli historii, co znacząco podnosi User Experience (UX). |

## 5. Kluczowe Funkcjonalności

1.  **Dynamiczne Skalowanie:** Użytkownik może w czasie rzeczywistym zwiększać lub zmniejszać liczbę workerów. System automatycznie zarządza procesami systemowymi.
2.  **Precyzyjny Pomiar Czasu:** Zegar ataku mierzy czas z dokładnością do **0.01s** (setnych sekundy), co pozwala na precyzyjne benchmarkowanie wydajności.
3.  **Archiwum Sukcesów:** Każde złamane hasło jest zapisywane w bazie danych wraz z metadanymi: hashem, algorytmem oraz liczbą workerów użytych do zadania.
4.  **Tryb Dockerowy:** Kompletna konfiguracja Dockerfile i Docker Compose pozwala na natychmiastowe wdrożenie środowiska produkcyjnego.

## 6. Opis Implementacji Kodu

*   **Algorytm Brute-force:** Zastosowano mapowanie indeksu liczbowego (`long`) na kombinację znaków. Pozwala to na bezstanowy podział pracy – serwer wysyła jedynie zakres indeksów (np. 1M - 2M), a worker generuje odpowiadające im hasła.
*   **Obsługa Bazy Danych:** W Singletonie `WorkQueueManager` wykorzystano `IServiceScopeFactory` do bezpiecznego zapisu wyników w bazach danych w asynchronicznym środowisku wielowątkowym.
*   **Cleanup:** Zaimplementowano mechanizm czyszczenia procesów potomnych przy zamykaniu serwera, co zapobiega wyciekom zasobów systemowych.

## 7. Wnioski
System **Password Breaker** udowadnia, że nowoczesne technologie .NET i React pozwalają na budowę narzędzi o dużej mocy obliczeniowej przy jednoczesnym zachowaniu lekkości i responsywności interfejsu. Projekt jest skalowalny horyzontalnie – dodanie kolejnych maszyn z workerami liniowo skraca czas potrzebny na odzyskanie hasła.
