# Amazon Picture Packager

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://buymeacoffee.com/spreed)

Amazon Picture Packager is a desktop utility built with Avalonia and C# that streamlines the process of creating image upload packages for Amazon listings. It is designed for sellers or integrators who regularly manage product images across multiple ASINs.

## ✨ Features

- Input multiple ASINs at once
- Choose a single image to apply to all ASINs
- Automatically creates ZIP archives per ASIN
- Output files are Amazon-ready for bulk image upload
- Cross-platform (Windows, Linux, macOS) via Avalonia

## 📦 Use Case

Let's say you're launching a set of identical products under different ASINs or updating a shared product image. Instead of manually creating and naming folders or archives, Amazon Picture Packager does it all in seconds.

## 🚀 How It Works

1. Start the application.
2. Paste or type in the list of ASINs (one per line, it recognizes correctly formatted strings).
3. Select the product image file (JPEG/PNG).
4. Choose which Amazon picture slot it should be.
   - Options include: `MAIN`, `PT01...`, `LC01...`, and so on.
5. Click "Pack!".
6. The tool creates ZIP files named for Amazon's mass upload process.

## 🖼️ Image Naming Convention

Each ZIP contains images named according to Amazon’s convention (e.g., `ASIN.SLOT.jpg`), ensuring compatibility with bulk upload tools.

## 🛠️ Requirements

- .NET9 or later
- Runs on Windows, Linux, and macOS

## 🔒 Privacy & Security

No internet connection is required. All processing is done locally on your machine.

## 👨‍💻 Development

Built with:

- [Avalonia UI](https://avaloniaui.net/)
- C# (.NET9 or later)

## Contributing
Contributions are welcome! Feel free to submit issues or pull requests.

## 📄 License

This project is licensed under the [MIT License](LICENSE.txt).

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://buymeacoffee.com/spreed)