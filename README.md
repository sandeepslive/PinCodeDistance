# PinCodeDistance API

## 📌 Overview
PinCodeDistance API calculates the **distance (in kilometers)** and **estimated travel duration** between two Indian pincodes using the **Google Maps API**.

---

## 🛠️ Features
- Fetches **latitude & longitude** for a given pincode.
- Calculates **Haversine distance** between two pincodes.
- Fetches **real-world distance and travel time** using Google Maps Distance Matrix API.
- Logs API calls and errors for debugging.

---

## 🚀 Getting Started
### 1️⃣ Prerequisites
- .NET 6 or later
- Google Cloud API Key with **Geocoding** and **Distance Matrix** API enabled

### 2️⃣ Installation
1. Clone the repository:
   ```sh
   git clone https://github.com/yourusername/PinCodeDistance.git
   cd PinCodeDistance
   ```
2. Install dependencies:
   ```sh
   dotnet restore
   ```
3. Set up your **Google API Key** in `appsettings.json`:
   ```json
   {
     "GoogleApiKey": "YOUR_GOOGLE_MAPS_API_KEY"
   }
   ```
4. Run the application:
   ```sh
   dotnet run
   ```

---

## 📌 API Endpoints
### 🔹 Get Distance Between Two Pincodes
**Endpoint:**  
`POST /api/pincode/distance`

**Request Body:**
```json
{
  "OriginPincode": "110001",
  "DestinationPincode": "560001"
}
```

**Response:**
```json
{
  "distance": 1740,
  "duration": "30h 15m",
  "distanceUnit": "km",
  "status": {
    "Code": 200,
    "text": "success"
  }
}
```

### 🔹 Get Distance Using V2 (Enhanced Calculation)
**Endpoint:**  
`POST /api/pincode/distancev2`

**Request Body:**
```json
{
  "OriginPincode": "110001",
  "DestinationPincode": "560001"
}
```

**Response:**
```json
{
  "distance": 1740,
  "distanceUnit": "km",
  "status": {
    "Code": 200,
    "text": "success"
  }
}
```

---

## 🔹 How It Works
1. **Fetch Geolocation:**
   - Calls Google **Geocoding API** to retrieve latitude & longitude for each pincode.
2. **Calculate Distance:**
   - Uses the **Haversine formula** for straight-line distance.
   - Calls **Google Distance Matrix API** for real-world travel distance & duration.
3. **Return Response:**
   - Returns distance (km) and estimated travel time.

---

## 🛠️ Tech Stack
- **.NET 6** (C#)
- **Google Maps APIs** (Geocoding + Distance Matrix)
- **Newtonsoft.Json** for JSON handling
- **Serilog** for logging

---

## 📜 License
This project is licensed under the **MIT License**.

---

## 🤝 Contributing
1. Fork the repository.
2. Create a new branch (`feature-branch`).
3. Commit changes and push.
4. Open a **Pull Request**!

---

## 📧 Contact
For questions, reach out at **your.email@example.com** or open an **issue** on GitHub!

