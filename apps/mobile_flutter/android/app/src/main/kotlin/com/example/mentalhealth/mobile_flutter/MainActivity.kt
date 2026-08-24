package com.example.mentalhealth.mobile_flutter

import android.content.ContentValues
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.File

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            "com.example.mentalhealth.mobile_flutter/data_export"
        ).setMethodCallHandler { call, result ->
            if (call.method != "saveDataExport") {
                result.notImplemented()
                return@setMethodCallHandler
            }

            val requestedName = call.argument<String>("fileName")
            val bytes = call.argument<ByteArray>("bytes")
            if (requestedName.isNullOrBlank() || bytes == null) {
                result.error("EXPORT_ARGUMENT_INVALID", "Missing export file data.", null)
                return@setMethodCallHandler
            }

            try {
                val fileName = safeZipName(requestedName)
                saveToDownloads(fileName, bytes)
                result.success(fileName)
            } catch (exception: Exception) {
                result.error(
                    "EXPORT_SAVE_FAILED",
                    exception.javaClass.simpleName,
                    null
                )
            }
        }
    }

    private fun saveToDownloads(fileName: String, bytes: ByteArray) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            val values = ContentValues().apply {
                put(MediaStore.Downloads.DISPLAY_NAME, fileName)
                put(MediaStore.Downloads.MIME_TYPE, "application/zip")
                put(MediaStore.Downloads.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS)
                put(MediaStore.Downloads.IS_PENDING, 1)
            }
            val resolver = applicationContext.contentResolver
            val uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
                ?: error("Download destination is unavailable.")
            try {
                resolver.openOutputStream(uri, "w")?.use { output ->
                    output.write(bytes)
                } ?: error("Download file cannot be opened.")
                values.clear()
                values.put(MediaStore.Downloads.IS_PENDING, 0)
                resolver.update(uri, values, null, null)
            } catch (exception: Exception) {
                resolver.delete(uri, null, null)
                throw exception
            }
            return
        }

        val downloads = getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS)
            ?: error("Download destination is unavailable.")
        downloads.mkdirs()
        File(downloads, fileName).writeBytes(bytes)
    }

    private fun safeZipName(requestedName: String): String {
        val baseName = requestedName.substringAfterLast('/').substringAfterLast('\\')
        val safeName = baseName.replace(Regex("[^A-Za-z0-9._-]"), "_")
        val finalName = if (safeName.endsWith(".zip", ignoreCase = true)) {
            safeName
        } else {
            "$safeName.zip"
        }
        require(finalName.length in 5..120) { "Invalid export file name." }
        return finalName
    }
}
