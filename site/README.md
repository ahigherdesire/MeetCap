# MeetCap landing page

A static site (HTML/CSS, no build step). Deploy anywhere that serves static files.

## 1. Configure it (one place)

Open `index.html` and edit the `window.MEETCAP` block near the top:

```js
window.MEETCAP = {
  stripeUrl:  "https://buy.stripe.com/...",   // your Stripe Payment Link
  downloadUrl:"https://.../MeetCap-Setup-1.0.0.exe", // where the installer is hosted
  price:      "$29",
  priceNote:  "one-time payment",
  contactEmail:"support@yourdomain.com"
};
```

Every Buy button uses `stripeUrl`; every Download button uses `downloadUrl`.

## 2. Host the installer

Put `MeetCap-Setup-1.0.0.exe` somewhere with a public URL and use it as `downloadUrl`:
- **GitHub Releases** (free): create a release on your repo, attach the .exe, copy its link.
- Or any file host / your own server / Cloudflare R2 / S3.

## 3. Set up Stripe (the paywall)

1. Create a **Stripe** account → switch on payments.
2. Stripe Dashboard → **Payment Links** → **New** → create a product (e.g. "MeetCap License – $29") → **Create link**.
3. Copy the link into `stripeUrl`.
4. (Recommended) In the payment link settings, enable **"Collect customer email"** so you get their address to send the key.

## 4. Fulfilling orders (shipping keys)

When you get a Stripe payment notification (email/dashboard):
1. Mint a unique key:
   ```
   dotnet run --project ../tools/KeyGen -- mint            # perpetual
   dotnet run --project ../tools/KeyGen -- mint --days 365 # 1-year
   ```
2. Email the key to the customer's address from the Stripe receipt.

> Each key is unique and signed; verify any key with
> `dotnet run --project ../tools/KeyGen -- verify <KEY>`.

## 5. Deploy the site

Upload the contents of this `site/` folder to any static host:
- **Cloudflare Pages**, **Netlify**, **Vercel**, or **GitHub Pages** — drag-and-drop or connect the repo.
- Point your domain at it.

## Files
- `index.html` — landing page
- `styles.css` — styling
- `terms.html`, `privacy.html` — legal pages (required by Stripe)
- `assets/` — logo and screenshots

## Later: automate key delivery
This flow ships keys manually. To automate, add a small serverless function (Stripe webhook →
mint key → email) or use a store like **Lemon Squeezy**/**Gumroad** that generates & emails license
keys for you. The app can also be upgraded to validate keys against their license API for revocation.
