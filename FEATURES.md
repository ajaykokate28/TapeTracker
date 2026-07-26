# TapeTracker — Complete Feature List

A modern .NET MAUI application for tailors to manage customers, measurements, orders, and delivery workflows — with multi-language support, OCR-driven data entry, QR-based sharing, and PIN-protected access.

---

## Customer Management

- **Add / Edit / Delete customers** with name, phone, order/bill number
- **Search** by name, phone, or order number (with smart phone match — strips non-digits and matches on the last 3–4 digits)
- **Recent customers view** on the home screen (most-recent additions surface first)
- **All Customers directory** page with full list, search, and status filter
- **Undo delete** — snackbar with `UNDO` for 5 seconds after removing a customer
- **Customer autocomplete** while typing on the new-measurement form (prevents duplicate customer records)
- **Family / Group Tag** — group multiple customers under one tag (e.g. "Sharma Family")
  - Tag autosuggest with prefix matching while typing
  - Case-insensitive de-duplication of tags
  - Automatic canonical-spelling adoption (won't create "sharma family" if "Sharma Family" already exists)
  - Filter customer list by tag via native dropdown
  - Family Members panel on Customer Detail — see all other members of the same family
  - Tap a family-member chip to jump straight to their details

---

## Measurement Form

- **Step-by-step wizard** (4 collapsible steps):
  1. Customer Info (name, phone, order #, order date, optional delivery date)
  2. Shirt measurements
  3. Pant measurements
  4. Notes (fabric, fitting preferences, delivery hints)
- **Full shirt measurements**: Length, Chest, Waist, Hip, Shoulder, Sleeve, Cuff, Collar
- **Full pant measurements**: Length, Waist, Hip, Thigh, Ankle, Knee, Seat
- **"Show / hide advanced fields"** toggle keeps the form short by default
- **Copy from last order** — one-tap to reuse the previous order's measurements
- **Unit toggle** (inches ↔ centimetres) with automatic label refresh
- **Auto-generate order number** button
- **Optional delivery-date checkbox** with tick-to-enable UX
- **Required-field validation** with inline hints

---

## Order Tracking

- **Per-customer order history** on the Customer Detail page
- **Order status pipeline**: Received → Cutting → Stitching → Ready → Delivered
- **Visual status stepper** on every order card
- **One-tap "Advance" status** button
- **Repeat order** — clone an existing order as the new one
- **Edit order** — modify measurements, notes, delivery date
- **Delete order** with confirmation
- **Delivery date tracking** with colour-coded urgency (overdue / due soon / on-time)

---

## Dashboard / Analytics

- **Business overview header** with 4 stat tiles: Customers, Total Orders, Due This Week, Overdue
- **Order pipeline bars** — visual breakdown across all 5 statuses with counts and percentages
- **6-month trend chart** (SkiaSharp bar chart of orders per month)
- **Recent Orders list** with tap-through to the customer
- **Pull-to-refresh** to reload stats

---

## OCR Scan

- **Camera capture** — take a photo of a paper measurement slip
- **Gallery import** — pick an existing photo
- **Automatic text extraction** and parsing of shirt/pant measurements
- **Review-and-edit** screen before saving (with field-count feedback)
- **View raw OCR text** for debugging
- **Cancel / Save** as a new customer + order

---

## Sharing / Export

- **QR code generation** per order — scannable summary (customer name, order #, status, date)
- **Share QR code** as PNG (native share sheet)
- **PDF bill generation** per order (SkiaSharp-rendered)
- **JSON export** of a single order (raw data / integration)
- **Excel export** of all customers/orders from the All Customers page
- **Notify** button (per order) for follow-up messaging

---

## Backup & Restore

- **One-tap backup** — full database dump saved to a file
- **Restore** from a previously saved backup file
- **Idempotent schema migration** on every startup (safely upgrades legacy databases without data loss)

---

## Security

- **PIN lock screen** — 4-digit PIN required on app launch (optional)
- **Set / change / remove PIN** from settings
- **Confirm-PIN flow** with mismatch protection

---

## UI / UX Polish

- **Splash screen** with animated logo, pulse rings, and loading dots
- **Modern gradient headers** on every page (indigo → violet → pink)
- **Light / Dark theme toggle** (top-right icon)
- **Multi-language support**: English, मराठी (Marathi), हिंदी (Hindi), ગુજરાતી (Gujarati)
  - Every label, placeholder, status pill, PIN prompt, and OCR message translates live
  - One-tap language pill in the header
- **Responsive card layouts** built to be crash-safe on WinUI 3 (no `Border.Shadow` inside virtualized items, `BoxView` instead of `Ellipse` in item templates, `FlexLayout` instead of horizontal `ScrollView` inside `CollectionView` cells)
- **Empty-state screens** with tips (Welcome page for first-time users, "No orders yet" for new customers)
- **Snackbar / toast notifications** for undo, save-success, and errors
- **Family/group tag chip** on customer cards (amber pill with tag emoji)
- **Initials avatar** for every customer (gradient background)
- **Global unhandled-exception logger** on Windows for post-mortem debugging

---

## Data Layer

- **SQLite** local database via Entity Framework Core
- **Automatic schema migration** with table-rebuild pattern for legacy column removal (`PRAGMA foreign_keys` aware)
- **Customer / Order / ShirtMeasurement / PantMeasurement** entities with proper relationships
- **Distinct-tags query** for autosuggest
- **Search with status + tag + phone filters** in a single database round-trip

---

## Tech Stack

| Layer | Technology |
| --- | --- |
| UI | .NET MAUI 9 (XAML, compiled bindings) |
| MVVM | CommunityToolkit.Mvvm (`[ObservableProperty]` + partial properties for WinRT AOT) |
| Database | SQLite via Entity Framework Core |
| Graphics | SkiaSharp (PDF generation, trend charts) |
| OCR | Windows.Media.Ocr (via platform service) |
| Target | Windows (WinUI 3, `net9.0-windows10.0.19041.0`) |
