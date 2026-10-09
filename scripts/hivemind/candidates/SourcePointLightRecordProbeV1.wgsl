@group(0) @binding(0) var<storage,read> source_record:array<u32>;
@group(0) @binding(1) var<storage,read_write> output:array<u32>;
@compute @workgroup_size(1) fn main(){
 output[0]=source_record[0]; output[1]=source_record[1];
 output[2]=source_record[2]; output[3]=source_record[3];
 output[4]=source_record[4]; output[5]=source_record[5];
 output[6]=source_record[6]; output[7]=source_record[7];
 output[8]=source_record[8]; output[9]=source_record[9];
 output[10]=source_record[10]; output[11]=source_record[11];
 output[12]=source_record[12]; output[13]=source_record[13];
 output[14]=source_record[14]; output[15]=source_record[15];
 output[16]=source_record[16]; output[17]=source_record[17];
 output[18]=source_record[18]; output[19]=source_record[19];
}
