import Foundation
import AVFoundation
import AppKit
import CoreVideo

let args=CommandLine.arguments
if args.count != 3 {fatalError("Usage: encode frame-directory output.mp4")}
let dir=URL(fileURLWithPath:args[1]),output=URL(fileURLWithPath:args[2])
let frames=try FileManager.default.contentsOfDirectory(at:dir,includingPropertiesForKeys:nil).filter{$0.pathExtension=="png"}.sorted{$0.lastPathComponent<$1.lastPathComponent}
if frames.isEmpty {fatalError("No frames")}
if FileManager.default.fileExists(atPath:output.path){try FileManager.default.removeItem(at:output)}
let writer=try AVAssetWriter(outputURL:output,fileType:.mp4)
let input=AVAssetWriterInput(mediaType:.video,outputSettings:[AVVideoCodecKey:AVVideoCodecType.h264,AVVideoWidthKey:720,AVVideoHeightKey:720,AVVideoCompressionPropertiesKey:[AVVideoAverageBitRateKey:2400000,AVVideoProfileLevelKey:AVVideoProfileLevelH264HighAutoLevel,AVVideoMaxKeyFrameIntervalKey:24]])
input.expectsMediaDataInRealTime=false
let adaptor=AVAssetWriterInputPixelBufferAdaptor(assetWriterInput:input,sourcePixelBufferAttributes:[kCVPixelBufferPixelFormatTypeKey as String:kCVPixelFormatType_32ARGB,kCVPixelBufferWidthKey as String:720,kCVPixelBufferHeightKey as String:720,kCVPixelBufferCGImageCompatibilityKey as String:true,kCVPixelBufferCGBitmapContextCompatibilityKey as String:true])
writer.add(input);writer.shouldOptimizeForNetworkUse=true
if !writer.startWriting(){fatalError(writer.error?.localizedDescription ?? "Cannot start writer")};writer.startSession(atSourceTime:.zero)
for (i,url) in frames.enumerated() {
    while !input.isReadyForMoreMediaData {Thread.sleep(forTimeInterval:0.003);if writer.status == .failed {fatalError(writer.error!.localizedDescription)}}
    try autoreleasepool {
        var pixel:CVPixelBuffer?;guard CVPixelBufferPoolCreatePixelBuffer(nil,adaptor.pixelBufferPool!,&pixel)==kCVReturnSuccess,let pixel=pixel else{fatalError("No pixel buffer")}
        CVPixelBufferLockBaseAddress(pixel,[])
        let bitmap=CGContext(data:CVPixelBufferGetBaseAddress(pixel),width:720,height:720,bitsPerComponent:8,bytesPerRow:CVPixelBufferGetBytesPerRow(pixel),space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.noneSkipFirst.rawValue)!
        let data=try Data(contentsOf:url);let source=CGImageSourceCreateWithData(data as CFData,nil)!;let image=CGImageSourceCreateImageAtIndex(source,0,nil)!
        bitmap.draw(image,in:CGRect(x:0,y:0,width:720,height:720))
        CVPixelBufferUnlockBaseAddress(pixel,[])
        if !adaptor.append(pixel,withPresentationTime:CMTime(value:Int64(i),timescale:24)){fatalError(writer.error?.localizedDescription ?? "Frame append failed")}
    }
}
input.markAsFinished();let sem=DispatchSemaphore(value:0);writer.finishWriting{sem.signal()};sem.wait()
if writer.status != .completed {fatalError(writer.error?.localizedDescription ?? "Writer incomplete")}
print("Encoded \(output.lastPathComponent): \(frames.count) frames")
