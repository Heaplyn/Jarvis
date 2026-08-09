import { 
    ChatInputCommandInteraction, 
    SlashCommandBuilder, 
    SlashCommandSubcommandsOnlyBuilder,
    SlashCommandOptionsOnlyBuilder
} from 'discord.js';

export interface Command {
    data: SlashCommandBuilder | SlashCommandSubcommandsOnlyBuilder | SlashCommandOptionsOnlyBuilder;
    execute: (interaction: ChatInputCommandInteraction) => Promise<void>;
}
