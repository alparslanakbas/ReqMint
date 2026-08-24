import AppKit
import Foundation

guard CommandLine.arguments.count == 2 else {
    FileHandle.standardError.write(Data("Usage: swift GenerateAppIcon.swift <output.png>\n".utf8))
    exit(1)
}

let outputUrl = URL(fileURLWithPath: CommandLine.arguments[1])
let canvas = NSSize(width: 1024, height: 1024)
let image = NSImage(size: canvas)

image.lockFocus()
NSGraphicsContext.current?.imageInterpolation = .high

let background = NSBezierPath(
    roundedRect: NSRect(x: 0, y: 0, width: 1024, height: 1024),
    xRadius: 220,
    yRadius: 220)
NSColor(calibratedRed: 15.0 / 255.0, green: 23.0 / 255.0, blue: 42.0 / 255.0, alpha: 1).setFill()
background.fill()

let mark = NSBezierPath(ovalIn: NSRect(x: 172, y: 172, width: 680, height: 680))
NSColor(calibratedRed: 52.0 / 255.0, green: 211.0 / 255.0, blue: 153.0 / 255.0, alpha: 1).setFill()
mark.fill()

let paragraph = NSMutableParagraphStyle()
paragraph.alignment = .center

let attributes: [NSAttributedString.Key: Any] = [
    .font: NSFont.systemFont(ofSize: 500, weight: .bold),
    .foregroundColor: NSColor(calibratedRed: 15.0 / 255.0, green: 23.0 / 255.0, blue: 42.0 / 255.0, alpha: 1),
    .paragraphStyle: paragraph
]

NSString(string: "R").draw(
    in: NSRect(x: 172, y: 220, width: 680, height: 590),
    withAttributes: attributes)
image.unlockFocus()

guard
    let tiff = image.tiffRepresentation,
    let representation = NSBitmapImageRep(data: tiff),
    let png = representation.representation(using: .png, properties: [:])
else {
    FileHandle.standardError.write(Data("Could not render the ReqMint app icon.\n".utf8))
    exit(1)
}

try png.write(to: outputUrl, options: .atomic)
