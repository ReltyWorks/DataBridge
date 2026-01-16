# DataBridge

**DataBridge** is a high-performance backend solution designed to aggregate Steam game data and provide an ultra-fast search engine for web applications.

It utilizes a **Dual-Database Architecture** that separates heavy data storage (MySQL) from a lightweight, high-speed search index (RAM), ensuring both data integrity and near-instantaneous search responses ($O(1)$) using a **Pruning Trie** algorithm.

## Key Features

* **Ultra-Fast Search:** Implements a **Pruning Trie (Top-k Weighted Trie)** algorithm to search through 100,000+ games in microseconds ($0.00ms$ latency).
* **Dual-DB Architecture:**
    * **Storage (Disk):** MySQL database for detailed game information (Description, Tags, Images).
    * **Index (RAM):** In-memory Dictionary & Trie structure for blazing fast lookups.
* **Two-Track Update Strategy:** Decouples data fetching (SteamCrawler) from data serving (DataBridge Server) to ensure high availability and stability.
* **gRPC & JSON Transcoding:** Built with ASP.NET Core gRPC, supporting both high-performance Protobuf communication and standard REST/JSON for web clients (e.g., PHP).
* **Smart Caching:** Automatic RAM caching of search indexes upon startup.

## Architecture

### 1. SteamCrawler (Data Collector)
A separate console application responsible for fetching data from the Steam API.
* **Logic:** Fetches the full app list, compares it with `History.txt`, and extracts only **newly released games**.
* **Output:** Generates `NewArrival.txt` for the main server to process.

### 2. DataBridge Server (Main Core)
The gRPC server that handles data ingestion and search queries.
* **Ingestion:** Detects `NewArrival.txt`, inserts data into MySQL, and updates the RAM Index immediately.
* **Search:** Handles user queries using the Weighted Pruning Trie to return the most relevant games (sorted by popularity/priority).

## Tech Stack

* **Core:** C# / .NET 8
* **Framework:** ASP.NET Core gRPC
* **Database:** MySQL (Storage), In-Memory (Search Index)
* **Communication:** gRPC (Protobuf), HTTP/2, REST (via JSON Transcoding)
* **External API:** Steam Web API

## Project Structure

```text
DataBridge/
...