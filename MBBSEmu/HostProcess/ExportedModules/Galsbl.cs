﻿using System;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System.Text;
using MBBSEmu.CPU;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Class which defines functions &amp; properties that are part of the Galacticomm
    ///     Global Software Breakout Library (GALGSBL.H). 
    /// </summary>
    public class Galsbl : ExportedModuleBase, IExportedModule
    {

        public Galsbl(MbbsModule module, PointerDictionary<UserSession> channelDictionary) : base(module, channelDictionary)
        {
            if(!Module.Memory.HasSegment((ushort)EnumHostSegments.Bturno))
                Module.Memory.AddSegment((ushort) EnumHostSegments.Bturno);
        }

        public ushort Invoke(ushort ordinal)
        {
            return ordinal switch
            {
                72 => bturno(),
                36 => btuoba(),
                49 => btutrg(),
                21 => btuinj(),
                60 => btuxnf(),
                39 => btupbc(),
                87 => btuica(),
                6 => btucli(),
                4 => btuchi(),
                63 => chious(),
                83 => btueba(),
                19 => btuiba(),
                _ => throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal: {ordinal}")
            };
        }

        public void SetState(CpuRegisters registers, ushort channelNumber)
        {
            Registers = registers;
            Module.Memory.SetWord((ushort)EnumHostSegments.UserNum, 0, channelNumber);
        }

        /// <summary>
        ///     8 digit + NULL GSBL Registration Number
        ///
        ///     Signature: char bturno[]
        ///     Result: DX == Segment containing bturno
        /// </summary>
        /// <returns></returns>
        public ushort bturno()
        {
            const string registrationNumber = "97771457\0";
            Module.Memory.SetArray((ushort)EnumHostSegments.Bturno, 0, Encoding.Default.GetBytes(registrationNumber));

            return (ushort) EnumHostSegments.Bturno;
        }

        /// <summary>
        ///     Report the amount of space (number of bytes) available in the output buffer
        ///     Since we're not using a dialup terminal or any of that, we'll just set it to ushort.MaxValue
        ///
        ///     Signature: int btuoba(int chan)
        ///     Result: AX == bytes available
        /// </summary>
        /// <returns></returns>
        public ushort btuoba()
        {
            Registers.AX = ushort.MaxValue;

            return 0;
        }

        /// <summary>
        ///     Set the input byte trigger quantity (used in conjunction with btuict())
        ///
        ///     Signature: int btutrg(int chan,int nbyt)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        public ushort btutrg()
        {
            //TODO -- Set callback for how characters should be processed

            Registers.AX = 0;

            return 0;
        }

        /// <summary>
        ///     Inject a status code into a channel
        /// 
        ///     Signature: int btuinj(int chan,int status)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        public ushort btuinj()
        {
            var channel = GetParameter(0);
            var status = GetParameter(1);

            //Status Change
            //Set the Memory Value
            Module.Memory.SetWord((ushort) EnumHostSegments.Status, 0, status);

            //Notify the Session that a Status Change has occured
            ChannelDictionary[channel].StatusChange = true;

            Registers.AX = 0;

            return 0;
        }

        /// <summary>
        ///     Set XON/XOFF characters, select page mode
        ///
        ///     Signature: int btuxnf(int chan,int xon,int xoff,...)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        public ushort btuxnf()
        {
            //Ignore this, we won't deal with XON/XOFF
            Registers.AX = 0;
            return 0;
        }

        /// <summary>
        ///     Set screen-pause character
        ///     Pauses the screen when in the output stream
        ///
        ///     Puts the screen in screen-pause mode
        ///     Signature: int err=btupbc(int chan, char pausch)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        public ushort btupbc()
        {
            //TODO -- Handle this?

            Registers.AX = 0;
            return 0;
        }

        /// <summary>
        ///     Input from a channel - reading in whatever bytes are available, up to a limit
        ///
        ///     Signature: int btuica(int chan,char *rdbptr,int max)
        ///     Result: AX == Number of input characters retrieved
        /// </summary>
        /// <returns></returns>
        public ushort btuica()
        {
            var channelNumber = GetParameter(0);
            var destinationOffset = GetParameter(1);
            var destinationSegment = GetParameter(2);
            var max = GetParameter(3);

            //Nothing to Input?
            if (ChannelDictionary[channelNumber].DataFromClient.Count == 0)
            {
                Registers.AX = 0;
                return 0;
            }

            ChannelDictionary[channelNumber].DataFromClient.TryDequeue(out var inputFromChannel);

            ReadOnlySpan<byte> inputFromChannelSpan = inputFromChannel;
            Module.Memory.SetArray(destinationSegment, destinationOffset, inputFromChannelSpan.Slice(0, max));
            Registers.AX = (ushort) (inputFromChannelSpan.Length < max ? inputFromChannelSpan.Length : max);
            return 0;
        }

        /// <summary>
        ///     Clears the input buffer
        ///
        ///     Since our input buffer is a queue, we'll just clear it
        /// 
        ///     Signature: int btucli(int chan)
        ///     Result: 
        /// </summary>
        /// <returns></returns>
        public ushort btucli()
        {
            var channelNumber = GetParameter(0);

            ChannelDictionary[channelNumber].DataFromClient.Clear();

            Registers.AX = 0;

            return 0;
        }

        /// <summary>
        ///     Sets Input Character Interceptor
        ///
        ///     Signature: int err=btuchi(int chan, char (*rouadr)())
        /// </summary>
        /// <returns></returns>
        public ushort btuchi()
        {

            var channel = GetParameter(0);
            var routineSegment = GetParameter(1);
            var routineOffset = GetParameter(2);

            if (routineOffset != 0 && routineSegment != 0)
                throw new Exception("BTUCHI only handles NULL for the time being");

            Registers.AX = 0;

            return 0;
        }

        /// <summary>
        ///     Echo buffer space available for bytes
        ///
        ///     Signature: int btueba(int chan)
        ///     Returns: 0 == buffer is full
        ///              1-254 == Buffer is between full and empty
        ///              255 == Buffer is full
        /// </summary>
        /// <returns></returns>
        public ushort btueba()
        {
            var channel = GetParameter(0);

            //Always return that the echo buffer is empty, as 
            //we send data immediately to the client when it's 
            //written to the echo buffer (see chious())
            Registers.AX = 255;
            return 0;
        }

        public ushort btuiba()
        {
            var channelNumber = GetParameter(0);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ushort.MaxValue - 1;
                return 0;
            }


            if (channel.DataFromClient.Count > 0)
            {
                Registers.AX = (ushort)channel.DataFromClient.Peek().Length;
            }

            Registers.AX = 0;
            return 0;
        }

        /// <summary>
        ///     String Output (via Echo Buffer)
        /// </summary>
        /// <returns></returns>
        public ushort chious()
        {
            
            var channel = GetParameter(0);
            var stringOffset = GetParameter(1);
            var stringSegment = GetParameter(2);

            ChannelDictionary[channel].DataToClient.Enqueue(Module.Memory.GetString(stringSegment, stringOffset).ToArray());

            return 0;
        }


    }
}