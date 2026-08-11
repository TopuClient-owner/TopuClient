package com.topuclient

import net.fabricmc.api.ClientModInitializer
import org.slf4j.LoggerFactory

class TopuClient : ClientModInitializer {
    private val logger = LoggerFactory.getLogger("TopuClient")

    override fun onInitializeClient() {
        logger.info("Topu Client initialized successfully!")
    }
}
