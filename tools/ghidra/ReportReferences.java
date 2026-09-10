// Reports bounded references to one virtual address.
// @category Rechaos

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;

public class ReportReferences extends GhidraScript {
    private static final int MAX_REFERENCES = 200;

    @Override
    protected void run() throws Exception {
        String[] arguments = getScriptArgs();
        if (arguments.length != 1) {
            printerr("Supply one virtual address.");
            return;
        }

        Address target = toAddr(arguments[0]);
        ReferenceIterator references = currentProgram.getReferenceManager().getReferencesTo(target);
        int count = 0;
        while (references.hasNext() && count < MAX_REFERENCES && !monitor.isCancelled()) {
            Reference reference = references.next();
            Instruction instruction = currentProgram.getListing().getInstructionContaining(reference.getFromAddress());
            Function function = currentProgram.getFunctionManager().getFunctionContaining(reference.getFromAddress());
            println(reference.getFromAddress()
                + (function == null ? "" : " in " + function.getEntryPoint() + " " + function.getName())
                + " " + reference.getReferenceType()
                + (instruction == null ? "" : ": " + instruction));
            count++;
        }

        if (count == 0) println("No references found.");
        else if (count == MAX_REFERENCES) println("Output capped at " + MAX_REFERENCES + " references.");
    }
}
